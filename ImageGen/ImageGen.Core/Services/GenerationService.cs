using System.Collections.Concurrent;
using System.Text.Json;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>
/// Creates runs (model x seed pairs) and generates their images in the background.
/// Each generation is saved to the file store and the database before <see cref="GenerationUpdated"/> is raised.
/// </summary>
public sealed class GenerationService(
    ISessionRepository sessions,
    IRunRepository runs,
    IProviderRouter providers,
    IImageFileStore files,
    ModelCatalogService catalog,
    ISeedSource seeds,
    AttachmentService attachments,
    TimeProvider timeProvider,
    ILogger<GenerationService> logger,
    SessionChangeNotifier changes,
    IDebugTrace? trace = null,
    IAsyncDelay? delay = null,
    VideoModelCatalogService? videoCatalog = null) : ISessionGenerationCoordinator
{
    private readonly IDebugTrace _trace = trace ?? NullDebugTrace.Instance;
    private readonly IAsyncDelay _delay = delay ?? new SystemAsyncDelay(timeProvider);

    public const int MaxParallelGenerations = 4;

    /// <summary>Poll interval of a queued job: starts at the minimum and grows to the maximum (ADR 0005).</summary>
    private static readonly TimeSpan PollMinInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollMaxInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan VideoPollMinInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan VideoPollMaxInterval = TimeSpan.FromSeconds(30);
    private const int MaxPollNetworkFailures = 5;
    private const int StateSaveMaxAttempts = 3;
    private static readonly TimeSpan RemoteCancelTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StateSaveRetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _providerSlots = new(StringComparer.Ordinal);
    private readonly object _lifecycleGate = new();
    private readonly Dictionary<Guid, SessionJobState> _sessionStates = [];
    private readonly ConcurrentDictionary<Guid, RunningJob> _running = new();

    /// <summary>Raised on a background thread after each saved state change.</summary>
    public event EventHandler<GenerationUpdatedEventArgs>? GenerationUpdated;

    /// <summary>Requests cancellation of an active generation. Local cancellation is reported only when terminal state is persisted.</summary>
    public async Task<GenerationCancelResult> CancelGenerationAsync(Guid generationId, CancellationToken cancellationToken)
    {
        if (!_running.TryGetValue(generationId, out var job)) return GenerationCancelResult.NotActive;
        if (job.Generation.IsFinished) return GenerationCancelResult.NotActive;
        var operation = job.GetOrStartUserCancel(() => CancelGenerationCoreAsync(job));
        return await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Requests cancellation of each active generation in a run and returns one outcome per generation.</summary>
    public async Task<IReadOnlyList<GenerationCancelItem>> CancelRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var jobs = _running.Values.Where(job => job.RunId == runId).ToArray();
        var results = await Task.WhenAll(jobs.Select(async job =>
        {
            GenerationCancelResult result;
            try
            {
                result = await CancelGenerationAsync(job.GenerationId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Cancellation of generation {GenerationId} in session {SessionId} failed.", job.GenerationId, job.SessionId);
                logger.LogDebug(ex, "Run cancellation detail for generation {GenerationId}.", job.GenerationId);
                result = GenerationCancelResult.CancelFailed;
            }

            return new GenerationCancelItem(job.GenerationId, result);
        })).ConfigureAwait(false);
        return results;
    }

    private async Task<GenerationCancelResult> CancelGenerationCoreAsync(RunningJob job)
    {
        var generation = job.Generation;
        if (generation.IsFinished) return GenerationCancelResult.NotActive;

        await job.Started.Task.ConfigureAwait(false);

        var requestedBeforeSubmit = job.TryCancelBeforeSubmission();
        if (requestedBeforeSubmit)
        {
            try
            {
                job.Source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                return GenerationCancelResult.NotActive;
            }
        }

        await job.SubmitSettled.Task.ConfigureAwait(false);

        var remote = job.GetRemoteJob();
        if (remote is null)
        {
            if (!requestedBeforeSubmit)
            {
                return GenerationCancelResult.CancelFailed;
            }

            try
            {
                await job.Task.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Local cancellation wait ended for generation {GenerationId}.", job.GenerationId);
            }

            return await PersistUserCancellationAsync(job).ConfigureAwait(false);
        }

        if (!job.TryBeginRemoteCancel()) return GenerationCancelResult.CancelFailed;

        CancelOutcome outcome;
        try
        {
            using var timeout = new CancellationTokenSource(RemoteCancelTimeout);
            outcome = await remote.Value.Provider.CancelAsync(remote.Value.Handle, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning("User cancellation of generation {GenerationId} in session {SessionId} failed.", job.GenerationId, job.SessionId);
            logger.LogDebug(ex, "User cancellation detail for generation {GenerationId}", job.GenerationId);
            return GenerationCancelResult.CancelFailed;
        }

        if (outcome == CancelOutcome.CancellationRequested)
        {
            await job.StateGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (generation.IsFinished) return GenerationCancelResult.NotActive;
                generation.CancelRequestedAt ??= timeProvider.GetUtcNow();
                await PersistStateWithRetryAsync(generation, CancellationToken.None).ConfigureAwait(false);
                PublishConfirmedState(generation);
            }
            finally
            {
                job.StateGate.Release();
            }

            logger.LogInformation("Cancellation requested for active generation {GenerationId} in session {SessionId}.", job.GenerationId, job.SessionId);
            return GenerationCancelResult.CancellationRequested;
        }

        if (outcome == CancelOutcome.AlreadyCompleted)
        {
            logger.LogInformation("Provider reports generation already completed; keeping watch for generation {GenerationId} in session {SessionId}.", job.GenerationId, job.SessionId);
            return GenerationCancelResult.NotActive;
        }

        if (outcome == CancelOutcome.NotFound)
        {
            await job.StateGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (generation.IsFinished) return GenerationCancelResult.NotActive;
                await job.Source.CancelAsync().ConfigureAwait(false);
                generation.Status = GenerationStatus.Cancelled;
                generation.CompletedAt = timeProvider.GetUtcNow();
                await PersistStateWithRetryAsync(generation, CancellationToken.None).ConfigureAwait(false);
                NotifyConfirmedState(generation);
                return GenerationCancelResult.Cancelled;
            }
            finally
            {
                job.StateGate.Release();
            }
        }

        try
        {
            job.Source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return GenerationCancelResult.NotActive;
        }

        return await PersistUserCancellationAsync(job).ConfigureAwait(false);
    }

    private async Task<GenerationCancelResult> PersistUserCancellationAsync(RunningJob job)
    {
        await job.StateGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var generation = job.Generation;
            if (generation.IsFinished) return GenerationCancelResult.NotActive;
            generation.Status = GenerationStatus.Cancelled;
            generation.CompletedAt = timeProvider.GetUtcNow();
            await PersistStateWithRetryAsync(generation, CancellationToken.None).ConfigureAwait(false);
            NotifyConfirmedState(generation);
            return GenerationCancelResult.Cancelled;
        }
        finally
        {
            job.StateGate.Release();
        }
    }

    /// <summary>Starts a run of the session with its image models and <c>RunsPerModel</c> shared seeds.</summary>
    public async Task<Run> StartRunAsync(Guid sessionId, string prompt, CancellationToken cancellationToken)
    {
        var result = await StartRunWithAttachmentsAsync(sessionId, prompt, [], cancellationToken);
        return result.Run ?? throw new InvalidOperationException("No model of the session takes the requested input.");
    }

    /// <summary>Starts one video generation per selected model, retaining the run settings and optional first frame.</summary>
    public async Task<Run> StartVideoRunAsync(
        Guid sessionId,
        string prompt,
        IReadOnlyList<ModelRef> models,
        VideoParameters parameters,
        Guid? firstFrameGenerationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(parameters);
        if (models.Count == 0)
        {
            throw new ArgumentException("At least one video model is required.", nameof(models));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parameters.DurationSeconds);
        var text = NormalizePrompt(prompt);
        using var lease = AcquireStartLease(sessionId);
        await GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var firstFrame = await ReadFirstFrameAsync(sessionId, firstFrameGenerationId, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Prompt = text,
            Origin = RunOrigin.Assistant,
            CreatedAt = now,
            VideoParameters = parameters,
            FirstFrameGenerationId = firstFrameGenerationId,
        };

        var generations = new List<Generation>(models.Count);
        for (var index = 0; index < models.Count; index++)
        {
            var model = models[index] ?? throw new ArgumentException("A video model reference cannot be null.", nameof(models));
            var supportsSeed = false;
            if (videoCatalog is not null)
            {
                try
                {
                    supportsSeed = (await videoCatalog.FindAsync(model, cancellationToken).ConfigureAwait(false))?.SupportsSeed == true;
                }
                catch (ProviderException)
                {
                    // An unavailable catalog does not prevent a video request; it means seed support is unknown.
                }
            }

            generations.Add(new Generation
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                SessionId = sessionId,
                ProviderId = model.ProviderId,
                ModelId = model.ModelId,
                Prompt = text,
                Seed = seeds.NextSeed(),
                SeedApplied = supportsSeed,
                Status = GenerationStatus.Queued,
                Kind = MediaKind.Video,
                DurationSeconds = parameters.DurationSeconds,
                CreatedAt = now,
                SortOrder = index,
            });
        }

        await runs.AddRunAsync(run, generations, cancellationToken).ConfigureAwait(false);
        changes.Notify(sessionId);
        logger.LogInformation("Video run {RunId} created in session {SessionId} with {GenerationCount} generations.", run.Id, sessionId, generations.Count);
        foreach (var generation in generations)
        {
            LogStatus(generation);
            Launch(
                generation,
                lease.State,
                (job, token) => ExecuteVideoAsync(generation, parameters, firstFrame, job, token),
                acquireProviderSlot: false);
        }

        return run;
    }

    /// <summary>
    /// Starts a run with the user's attached images as reference images (task 018). A model that takes no references is
    /// skipped with a reason; a model with a limit gets the first attachments up to its limit. Returns no run when every
    /// model was skipped.
    /// </summary>
    public async Task<StartRunResult> StartRunWithAttachmentsAsync(
        Guid sessionId,
        string prompt,
        IReadOnlyList<Guid> attachmentIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachmentIds);
        var text = NormalizePrompt(prompt);
        using var lease = AcquireStartLease(sessionId);
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session.Settings.ImageModels.Count == 0)
        {
            throw new InvalidOperationException($"Session {sessionId} has no image models.");
        }

        var seedValues = Enumerable.Range(0, session.Settings.RunsPerModel).Select(_ => seeds.NextSeed()).ToArray();
        var pairs = new List<(ModelRef Model, long Seed, IReadOnlyList<Guid> References)>();
        var skipped = new List<SkippedModel>();
        var notSent = 0;
        foreach (var model in session.Settings.ImageModels)
        {
            var limit = ReferenceImageLimit.Of(await ReadModelAsync(model, cancellationToken));
            if (attachmentIds.Count > 0 && limit == 0)
            {
                skipped.Add(new SkippedModel(model, ModelSkipReason.NoReferenceSupport));
                continue;
            }

            var references = attachmentIds.Take(limit).ToList();
            notSent = Math.Max(notSent, attachmentIds.Count - references.Count);
            foreach (var seed in seedValues)
            {
                pairs.Add((model, seed, references));
            }
        }

        if (pairs.Count == 0)
        {
            return new StartRunResult(null, skipped, notSent);
        }

        if (_trace.IsEnabled)
        {
            RecordGenerationRequest(sessionId, text, attachmentIds, session.Settings, seedValues);
        }

        var run = await CreateRunAsync(sessionId, text, RunOrigin.Assistant, pairs, session.Settings.Parameters, cancellationToken, lease);
        return new StartRunResult(run, skipped, notSent);
    }

    /// <summary>The request as the user made it (task 085): prompt, models, seeds, parameters and attachment ids.</summary>
    private void RecordGenerationRequest(
        Guid sessionId,
        string text,
        IReadOnlyList<Guid> attachmentIds,
        SessionSettings settings,
        IReadOnlyList<long> seedValues)
    {
        var parameters = settings.Parameters;
        _trace.Record(new TraceRecord(
            "generation-request",
            new Dictionary<string, string?>
            {
                ["session"] = sessionId.ToString("N"),
                ["prompt"] = text,
                ["models"] = string.Join(",", settings.ImageModels.Select(m => $"{m.ProviderId}/{m.ModelId}")),
                ["seeds"] = string.Join(",", seedValues),
                ["aspectRatio"] = parameters.AspectRatio,
                ["resolution"] = parameters.Resolution,
                ["outputFormat"] = parameters.OutputFormat,
                ["quality"] = parameters.Quality,
                ["attachments"] = string.Join(",", attachmentIds.Select(id => id.ToString("N"))),
            },
            []));
    }

    /// <summary>New run from one generation: same prompt, model and seed.</summary>
    public async Task<Run> RepeatAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var source = await GetSourceAsync(generationId, cancellationToken);
        using var lease = AcquireStartLease(source.SessionId);
        var requested = await GetRunParametersAsync(source.RunId, cancellationToken);
        return await CreateRunAsync(source.SessionId, source.Prompt, RunOrigin.Repeat,
            [(SourceModel(source), source.Seed, source.ReferenceAttachmentIds)], requested, cancellationToken, lease);
    }

    /// <summary>New run from one generation: same prompt and model, new random seed.</summary>
    public async Task<Run> NewSeedAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var source = await GetSourceAsync(generationId, cancellationToken);
        using var lease = AcquireStartLease(source.SessionId);
        var requested = await GetRunParametersAsync(source.RunId, cancellationToken);
        return await CreateRunAsync(source.SessionId, source.Prompt, RunOrigin.NewSeed,
            [(SourceModel(source), seeds.NextSeed(), source.ReferenceAttachmentIds)], requested, cancellationToken, lease);
    }

    /// <summary>New run from one generation: new prompt, same model and seed.</summary>
    public async Task<Run> EditAsync(Guid generationId, string newPrompt, CancellationToken cancellationToken)
    {
        var text = NormalizePrompt(newPrompt);
        var source = await GetSourceAsync(generationId, cancellationToken);
        using var lease = AcquireStartLease(source.SessionId);
        var requested = await GetRunParametersAsync(source.RunId, cancellationToken);
        return await CreateRunAsync(source.SessionId, text, RunOrigin.Edit,
            [(SourceModel(source), source.Seed, source.ReferenceAttachmentIds)], requested, cancellationToken, lease);
    }

    /// <summary>New run from one generation on a model the user picked: same prompt and seed, a different model (task 076).</summary>
    public async Task<Run> RunOnModelAsync(Guid generationId, ModelRef model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var source = await GetSourceAsync(generationId, cancellationToken);
        using var lease = AcquireStartLease(source.SessionId);
        var requested = await GetRunParametersAsync(source.RunId, cancellationToken);
        if (source.ReferenceAttachmentIds.Count > 0)
        {
            var target = await ReadModelAsync(model, cancellationToken);
            var limit = ReferenceImageLimit.Of(target);
            if (limit == 0)
            {
                throw new ProviderException(ErrorKind.ModelWithoutReferences, "The selected model does not support reference images.");
            }

            if (source.ReferenceAttachmentIds.Count > limit)
            {
                throw new ProviderException(
                    ErrorKind.BadRequest,
                    $"The selected model accepts up to {limit} reference images, but {source.ReferenceAttachmentIds.Count} were provided.");
            }
        }

        return await CreateRunAsync(source.SessionId, source.Prompt, RunOrigin.OtherModel,
            [(model, source.Seed, source.ReferenceAttachmentIds)], requested, cancellationToken, lease);
    }

    /// <summary>Marks generations left unfinished by a previous app session as Failed with <see cref="ErrorKind.Interrupted"/>.</summary>
    /// <returns>Number of updated generations: the ones whose provider is gone plus the ones marked as interrupted.</returns>
    /// <remarks>
    /// Generations that have a queued job (a saved handle) are not interrupted: their jobs are followed again with the
    /// saved handle, without a new submit. A job of a provider that is no longer configured cannot be continued at all:
    /// such a generation ends with <see cref="ErrorKind.ModelUnavailable"/>. A provider that exists but does not run
    /// queued jobs is left as it is.
    /// </remarks>
    public async Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        var recovered = 0;
        foreach (var generation in await runs.ListResumableJobsAsync(cancellationToken))
        {
            StartLease lease;
            try
            {
                lease = AcquireStartLease(generation.SessionId);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            using (lease)
            {
                if (generation.Kind == MediaKind.Video)
                {
                    IQueuedVideoProvider queuedVideo;
                    try
                    {
                        queuedVideo = providers.GetQueuedVideoProvider(generation.ProviderId);
                    }
                    catch (ProviderException ex) when (ex.Kind == ErrorKind.ModelUnavailable)
                    {
                        generation.Status = GenerationStatus.Failed;
                        generation.ErrorKind = ErrorKind.ModelUnavailable;
                        generation.ErrorMessage = ex.Message;
                        generation.CompletedAt = timeProvider.GetUtcNow();
                        await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
                        recovered++;
                        continue;
                    }

                    ProviderJobHandle? videoHandle = null;
                    try
                    {
                        videoHandle = JsonSerializer.Deserialize<ProviderJobHandle>(generation.JobJson!);
                    }
                    catch (JsonException)
                    {
                        // WatchVideoAsync persists malformed handles as failed without resubmitting the job.
                    }

                    Launch(
                        generation,
                        lease.State,
                        (job, token) => WatchVideoAsync(generation, queuedVideo, videoHandle, job, token),
                        submitAlreadySettled: true,
                        acquireProviderSlot: false);
                    continue;
                }

                IQueuedImageProvider queued;
                try
                {
                    queued = providers.GetQueuedImageProvider(generation.ProviderId);
                }
                catch (ProviderException ex) when (ex.Kind == ErrorKind.ModelUnavailable)
                {
                    // The provider of the saved job is not configured: the job cannot be continued and must not hang.
                    generation.Status = GenerationStatus.Failed;
                    generation.ErrorKind = ErrorKind.ModelUnavailable;
                    generation.ErrorMessage = ex.Message;
                    generation.CompletedAt = timeProvider.GetUtcNow();
                    await SaveAndNotifyAsync(generation).ConfigureAwait(false);
                    recovered++;
                    continue;
                }

                ProviderJobHandle? recoveredHandle = null;
                try
                {
                    recoveredHandle = JsonSerializer.Deserialize<ProviderJobHandle>(generation.JobJson!);
                }
                catch (JsonException)
                {
                    // WatchQueuedAsync preserves the existing malformed-handle failure behavior.
                }

                Launch(generation, lease.State, (job, token) => WatchQueuedAsync(generation, queued, job, token), queued, recoveredHandle, submitAlreadySettled: true);
            }
        }

        return recovered + await runs.MarkUnfinishedAsInterruptedAsync(timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Waits until no background generation is running. Used by tests.</summary>
    public async Task WhenIdleAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (_lifecycleGate)
            {
                pending = _running.Values.Select(job => job.Task).ToArray();
            }

            if (pending.Length == 0)
            {
                return;
            }

            await Task.WhenAll(pending);
        }
    }

    public Task StopSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        Task stop;
        SessionJobState state;
        var startStop = false;
        lock (_lifecycleGate)
        {
            state = StateOf(sessionId);
            if (!state.IsDeleting)
            {
                state.IsDeleting = true;
                state.StopCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                startStop = true;
            }

            stop = state.StopCompletion!.Task;
        }

        if (startStop)
        {
            _ = CompleteStopAsync(state);
        }

        return stop.WaitAsync(cancellationToken);
    }

    private async Task CompleteStopAsync(SessionJobState state)
    {
        Exception? cancellationFailure = null;
        try
        {
            await state.StartsDrained.Task.ConfigureAwait(false);
            RunningJob[] jobs;
            lock (_lifecycleGate)
            {
                jobs = _running.Values.Where(job => job.SessionId == state.SessionId).ToArray();
            }

            try
            {
                state.StopSource.Cancel();
            }
            catch (Exception ex)
            {
                cancellationFailure = ex;
            }

            foreach (var job in jobs)
            {
                await job.SubmitSettled.Task.ConfigureAwait(false);
                var remoteJob = job.GetRemoteJob();
                if (remoteJob is null)
                {
                    continue;
                }

                await CancelRemoteJobBestEffortAsync(job, remoteJob.Value.Provider, remoteJob.Value.Handle).ConfigureAwait(false);
                if (remoteJob.Value.Provider is InMemoryQueuedImageProvider or InMemoryQueuedImageConnection)
                {
                    await WaitForLocalJobTerminalAsync(remoteJob.Value.Provider, remoteJob.Value.Handle).ConfigureAwait(false);
                }
            }

            await Task.WhenAll(jobs.Select(job => job.Task)).ConfigureAwait(false);
            if (cancellationFailure is not null)
            {
                throw cancellationFailure;
            }

            state.StopCompletion!.TrySetResult();
        }
        catch (Exception ex)
        {
            state.StopCompletion!.TrySetException(ex);
        }
        finally
        {
            state.StopSource.Dispose();
        }
    }

    private StartLease AcquireStartLease(Guid sessionId)
    {
        lock (_lifecycleGate)
        {
            var state = StateOf(sessionId);
            if (state.IsDeleting)
            {
                throw new InvalidOperationException("The session is being deleted.");
            }

            if (state.StartLeaseCount++ == 0)
            {
                state.StartsDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return new StartLease(this, state);
        }
    }

    private SessionJobState StateOf(Guid sessionId)
    {
        if (!_sessionStates.TryGetValue(sessionId, out var state))
        {
            state = new SessionJobState(sessionId);
            _sessionStates.Add(sessionId, state);
        }

        return state;
    }

    private void ReleaseStartLease(SessionJobState state)
    {
        lock (_lifecycleGate)
        {
            if (--state.StartLeaseCount == 0)
            {
                state.StartsDrained.TrySetResult();
            }
        }
    }

    private static string NormalizePrompt(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return prompt.Trim();
    }

    private static ModelRef SourceModel(Generation source) => new(source.ProviderId, source.ModelId);

    private async Task<Session> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await sessions.GetAsync(sessionId, cancellationToken)
        ?? throw new InvalidOperationException($"Session {sessionId} not found.");

    private async Task<Generation> GetSourceAsync(Guid generationId, CancellationToken cancellationToken) =>
        await runs.GetGenerationAsync(generationId, cancellationToken)
        ?? throw new InvalidOperationException($"Generation {generationId} not found.");

    /// <summary>Parameters the source run asked for. Repeat, new seed and edit use them, not the current session settings.</summary>
    private async Task<GenerationParameters> GetRunParametersAsync(Guid runId, CancellationToken cancellationToken) =>
        (await runs.GetRunAsync(runId, cancellationToken))?.Parameters
        ?? throw new InvalidOperationException($"Run {runId} not found.");

    private async Task<Run> CreateRunAsync(
        Guid sessionId,
        string prompt,
        RunOrigin origin,
        IReadOnlyList<(ModelRef Model, long Seed, IReadOnlyList<Guid> References)> pairs,
        GenerationParameters requested,
        CancellationToken cancellationToken,
        StartLease lease)
    {
        var now = timeProvider.GetUtcNow();
        var run = new Run
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Prompt = prompt,
            Origin = origin,
            CreatedAt = now,
            Parameters = requested,
        };

        var models = new Dictionary<ModelRef, ImageModel?>();
        var generations = new List<Generation>(pairs.Count);
        for (var index = 0; index < pairs.Count; index++)
        {
            var (model, seed, references) = pairs[index];
            if (!models.TryGetValue(model, out var image))
            {
                image = await ReadModelAsync(model, cancellationToken);
                models[model] = image;
            }

            generations.Add(new Generation
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                SessionId = sessionId,
                ProviderId = model.ProviderId,
                ModelId = model.ModelId,
                Prompt = prompt,
                Seed = seed,
                SeedApplied = image?.SupportsSeed ?? false,
                Status = GenerationStatus.Queued,
                CreatedAt = now,
                SortOrder = index,
                Outcomes = GenerationParameterPlanner.Plan(requested, image),
                ReferenceAttachmentIds = references,
            });
        }

        await runs.AddRunAsync(run, generations, cancellationToken);
        changes.Notify(sessionId);
        logger.LogInformation(
            "Run {RunId} created in session {SessionId} ({Origin}): {GenerationCount} generations.",
            run.Id,
            sessionId,
            origin,
            generations.Count);
        foreach (var generation in generations)
        {
            LogStatus(generation);
            Launch(generation, lease.State, (job, token) => ExecuteAsync(generation, job, token));
        }

        return run;
    }

    /// <summary>An unknown provider or model gives null: no seed is sent and every requested parameter is dropped as unknown.</summary>
    private async Task<ImageModel?> ReadModelAsync(ModelRef model, CancellationToken cancellationToken)
    {
        try
        {
            return await catalog.FindAsync(model, cancellationToken);
        }
        catch (ProviderException)
        {
            return null;
        }
    }

    private void Launch(
        Generation generation,
        SessionJobState state,
        Func<RunningJob, CancellationToken, Task> work,
        IQueuedImageProvider? queuedProvider = null,
        ProviderJobHandle? submittedHandle = null,
        bool submitAlreadySettled = false,
        bool acquireProviderSlot = true)
    {
        lock (_lifecycleGate)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(state.StopSource.Token);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var job = new RunningJob(generation, source);
            if (queuedProvider is not null && submittedHandle is not null)
            {
                job.PublishHandle(queuedProvider, submittedHandle);
            }

            if (submitAlreadySettled)
            {
                job.CompleteSubmitPhase();
            }

            job.Started.TrySetResult();

            if (!_running.TryAdd(generation.Id, job))
            {
                source.Dispose();
                return;
            }

            job.Task = Task.Run(async () =>
            {
                await gate.Task.ConfigureAwait(false);
                try
                {
                    if (acquireProviderSlot)
                    {
                        await RunWithProviderSlotAsync(generation.ProviderId, job, source.Token, work).ConfigureAwait(false);
                    }
                    else
                    {
                        await work(job, source.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (source.IsCancellationRequested)
                {
                    // Session stop is an expected job outcome, not a generation failure.
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        "Generation {GenerationId} in session {SessionId} from provider {ProviderId} failed at status {Status}.",
                        generation.Id,
                        generation.SessionId,
                        generation.ProviderId,
                        generation.Status);
                    logger.LogDebug(ex, "Generation failure detail {GenerationId}", generation.Id);
                }
                finally
                {
                    job.CompleteSubmitPhase();
                    lock (_lifecycleGate)
                    {
                        _running.TryRemove(job.GenerationId, out _);
                    }

                    source.Dispose();
                }
            });

            gate.SetResult();
        }
    }

    private async Task RunWithProviderSlotAsync(
        string providerId,
        RunningJob job,
        CancellationToken cancellationToken,
        Func<RunningJob, CancellationToken, Task> work)
    {
        var slots = _providerSlots.GetOrAdd(providerId, _ => new SemaphoreSlim(MaxParallelGenerations, MaxParallelGenerations));
        var acquired = false;
        try
        {
            await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            await work(job, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (acquired)
            {
                slots.Release();
            }
        }
    }

    private async Task ExecuteVideoAsync(
        Generation generation,
        VideoParameters parameters,
        ImageReference? firstFrame,
        RunningJob job,
        CancellationToken cancellationToken)
    {
        job.MarkPreparing();
        if (job.IsUserCancelRequested)
        {
            job.CompleteSubmitPhase();
            return;
        }

        IQueuedVideoProvider queued;
        try
        {
            queued = providers.GetQueuedVideoProvider(generation.ProviderId);
        }
        catch (ProviderException exception) when (exception.Kind == ErrorKind.ModelUnavailable)
        {
            generation.Status = GenerationStatus.Failed;
            generation.ErrorKind = exception.Kind;
            generation.ErrorMessage = exception.Message;
            generation.CompletedAt = timeProvider.GetUtcNow();
            job.CompleteSubmitPhase();
            await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
            return;
        }

        generation.Status = GenerationStatus.Queued;
        await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
        ProviderJobHandle? handle = null;
        try
        {
            await RunWithProviderSlotAsync(generation.ProviderId, job, cancellationToken, async (_, token) =>
            {
                if (!job.TryBeginSubmission())
                {
                    token.ThrowIfCancellationRequested();
                    return;
                }

                var request = new VideoGenerationRequest(
                    generation.ModelId,
                    generation.Prompt,
                    parameters.DurationSeconds,
                    parameters.Resolution,
                    parameters.AspectRatio,
                    parameters.GenerateAudio,
                    generation.SeedApplied ? generation.Seed : null,
                    firstFrame);
                handle = await queued.SubmitAsync(request, token).ConfigureAwait(false);
                generation.JobJson = JsonSerializer.Serialize(handle);
                generation.Status = GenerationStatus.Running;
                await SaveAndNotifyAsync(generation, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (generation.JobJson is null)
            {
                generation.Status = GenerationStatus.Failed;
                generation.ErrorKind = exception is ProviderException providerException ? providerException.Kind : ErrorKind.Unknown;
                generation.ErrorMessage = exception.Message;
                generation.CompletedAt = timeProvider.GetUtcNow();
                await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
                return;
            }

            throw;
        }
        finally
        {
            job.CompleteSubmitPhase();
        }

        if (handle is not null && !generation.IsFinished)
        {
            await WatchVideoAsync(generation, queued, handle, job, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WatchVideoAsync(
        Generation generation,
        IQueuedVideoProvider queued,
        ProviderJobHandle? handle,
        RunningJob job,
        CancellationToken cancellationToken)
    {
        if (handle is null)
        {
            try
            {
                handle = JsonSerializer.Deserialize<ProviderJobHandle>(generation.JobJson!)
                    ?? throw new InvalidOperationException("The saved video job handle is empty.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                await FailGenerationAsync(generation, ErrorKind.Unknown, exception.Message, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        var interval = VideoPollMinInterval;
        var networkFailures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            VideoJobUpdate update;
            try
            {
                update = await queued.PollAsync(handle, cancellationToken).ConfigureAwait(false);
                networkFailures = 0;
            }
            catch (ProviderException exception) when (exception.Kind == ErrorKind.Network)
            {
                networkFailures++;
                if (networkFailures >= MaxPollNetworkFailures)
                {
                    await job.StateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await ApplyPollingFailureAsync(generation, exception, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        job.StateGate.Release();
                    }

                    return;
                }

                await _delay.DelayAsync(interval, cancellationToken).ConfigureAwait(false);
                interval = GrowPollInterval(interval, VideoPollMaxInterval);
                continue;
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                await job.StateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await ApplyPollingFailureAsync(generation, exception, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    job.StateGate.Release();
                }

                return;
            }

            await job.StateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (generation.IsFinished)
                {
                    return;
                }

                if (!await ApplyVideoUpdateAsync(generation, queued, handle, update, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            finally
            {
                job.StateGate.Release();
            }

            await _delay.DelayAsync(interval, cancellationToken).ConfigureAwait(false);
            interval = GrowPollInterval(interval, VideoPollMaxInterval);
        }
    }

    /// <returns>True when the generation remains active and should be polled again.</returns>
    private async Task<bool> ApplyVideoUpdateAsync(
        Generation generation,
        IQueuedVideoProvider queued,
        ProviderJobHandle handle,
        VideoJobUpdate update,
        CancellationToken cancellationToken)
    {
        if ((update.State == JobState.Queued && generation.Status == GenerationStatus.Queued)
            || (update.State == JobState.Running && generation.Status == GenerationStatus.Running)
            || (update.State == JobState.Queued && generation.Status == GenerationStatus.Running))
        {
            return true;
        }

        switch (update.State)
        {
            case JobState.Queued:
                generation.Status = GenerationStatus.Queued;
                break;
            case JobState.Running:
                generation.Status = GenerationStatus.Running;
                break;
            case JobState.Succeeded:
                await SaveVideoResultAndPersistAsync(generation, queued, handle, update.CostUsd, cancellationToken).ConfigureAwait(false);
                return false;
            case JobState.Filtered:
                generation.Status = GenerationStatus.Filtered;
                generation.ErrorKind = ErrorKind.Moderated;
                generation.ErrorMessage = update.ErrorMessage;
                generation.CompletedAt = timeProvider.GetUtcNow();
                break;
            case JobState.Failed:
                generation.Status = GenerationStatus.Failed;
                generation.ErrorKind = update.ErrorKind ?? ErrorKind.Unknown;
                generation.ErrorMessage = update.ErrorMessage;
                generation.CostUsd = update.CostUsd;
                generation.CompletedAt = timeProvider.GetUtcNow();
                break;
            case JobState.Cancelled:
                generation.Status = GenerationStatus.Cancelled;
                generation.CompletedAt = timeProvider.GetUtcNow();
                break;
        }

        await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
        return !generation.IsFinished;
    }

    private async Task SaveVideoResultAndPersistAsync(
        Generation generation,
        IQueuedVideoProvider queued,
        ProviderJobHandle handle,
        decimal? costUsd,
        CancellationToken cancellationToken)
    {
        generation.CostUsd = costUsd;
        string relativePath;
        string mediaType;
        try
        {
            await using var content = await queued.OpenContentAsync(handle, cancellationToken).ConfigureAwait(false);
            mediaType = content.MediaType;
            relativePath = await files.SaveMediaStreamAsync(
                generation.SessionId,
                generation.Id,
                content.Content,
                mediaType,
                ProviderResponseLimits.Video,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            generation.Status = GenerationStatus.Failed;
            generation.ErrorKind = exception is ProviderException providerException ? providerException.Kind : ErrorKind.Unknown;
            generation.ErrorMessage = exception.Message;
            generation.CompletedAt = timeProvider.GetUtcNow();
            await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
            return;
        }

        generation.ImagePath = relativePath;
        generation.MediaType = mediaType;
        generation.Status = GenerationStatus.Succeeded;
        generation.CompletedAt = timeProvider.GetUtcNow();
        try
        {
            await PersistStateWithRetryAsync(generation, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await files.DeleteFileAsync(relativePath, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                logger.LogWarning("Could not clean up video file for generation {GenerationId} in session {SessionId}.", generation.Id, generation.SessionId);
                logger.LogDebug(cleanupException, "Video file cleanup detail for generation {GenerationId}: {RelativePath}", generation.Id, relativePath);
            }
            finally
            {
                generation.ImagePath = null;
                generation.MediaType = null;
            }

            throw;
        }

        NotifyConfirmedState(generation);
    }

    private async Task<ImageReference?> ReadFirstFrameAsync(
        Guid sessionId,
        Guid? generationId,
        CancellationToken cancellationToken)
    {
        if (generationId is null)
        {
            return null;
        }

        var generation = await runs.GetGenerationAsync(generationId.Value, cancellationToken).ConfigureAwait(false);
        if (generation is null
            || generation.SessionId != sessionId
            || generation.Kind != MediaKind.Image
            || generation.Status != GenerationStatus.Succeeded
            || string.IsNullOrWhiteSpace(generation.ImagePath)
            || string.IsNullOrWhiteSpace(generation.MediaType)
            || !generation.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The first frame must be a successful image generation in the same session.", nameof(generationId));
        }

        try
        {
            var bytes = await files.ReadAsync(generation.ImagePath, cancellationToken).ConfigureAwait(false);
            return new ImageReference(bytes, generation.MediaType);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("The selected first-frame image could not be read.", nameof(generationId), exception);
        }
    }

    private static TimeSpan GrowPollInterval(TimeSpan current, TimeSpan maximum)
    {
        var doubled = current + current;
        return doubled > maximum ? maximum : doubled;
    }

    private async Task ExecuteAsync(Generation generation, RunningJob job, CancellationToken cancellationToken)
    {
        job.MarkPreparing();
        if (job.IsUserCancelRequested)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        IQueuedImageProvider queued;
        try
        {
            queued = providers.GetQueuedImageProvider(generation.ProviderId);
        }
        catch (ProviderException ex) when (ex.Kind == ErrorKind.ModelUnavailable)
        {
            generation.Status = GenerationStatus.Failed;
            generation.ErrorKind = ErrorKind.ModelUnavailable;
            generation.ErrorMessage = ex.Message;
            generation.CompletedAt = timeProvider.GetUtcNow();
            await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
            return;
        }

        await ExecuteQueuedAsync(generation, queued, job, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Provider execution uses one queued lifecycle: the slot is held while the job is submitted; the watch runs after
    /// the slot is released, because native queues can take minutes.
    /// </summary>
    private async Task ExecuteQueuedAsync(Generation generation, IQueuedImageProvider queued, RunningJob job, CancellationToken cancellationToken)
    {
        var submitted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            generation.Status = GenerationStatus.Queued;
            await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
            await SubmitQueuedAsync(generation, queued, job, cancellationToken).ConfigureAwait(false);
            submitted = generation.JobJson is not null;
        }
        finally
        {
            // Signal submit settlement even when preparation was cancelled; the provider slot belongs to the job wrapper.
            if (!submitted)
            {
                job.CompleteSubmitPhase();
            }
        }

        if (!generation.IsFinished && generation.JobJson is not null)
        {
            await WatchQueuedAsync(generation, queued, job, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SubmitQueuedAsync(Generation generation, IQueuedImageProvider queued, RunningJob job, CancellationToken cancellationToken)
    {
        var request = await BuildRequestAsync(generation, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            generation.CompletedAt = timeProvider.GetUtcNow();
            await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (job.IsUserCancelRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        ProviderJobHandle? submittedHandle = null;
        try
        {
            if (!job.TryBeginSubmission()) cancellationToken.ThrowIfCancellationRequested();
            var handle = await queued.SubmitAsync(request, cancellationToken).ConfigureAwait(false);
            submittedHandle = handle;
            job.PublishHandle(queued, handle);
            job.CompleteSubmitPhase();
            generation.JobJson = JsonSerializer.Serialize(handle);
            generation.Status = queued is InMemoryQueuedImageProvider or InMemoryQueuedImageConnection
                ? GenerationStatus.Running
                : GenerationStatus.Queued;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ProviderException ex)
        {
            generation.Status = ex.Kind == ErrorKind.Moderated ? GenerationStatus.Filtered : GenerationStatus.Failed;
            generation.ErrorKind = ex.Kind;
            generation.ErrorMessage = ex.Message;
            generation.CompletedAt = timeProvider.GetUtcNow();
        }

        try
        {
            await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
        }
        catch when (submittedHandle is not null)
        {
            await CancelRemoteJobBestEffortAsync(job, queued, submittedHandle).ConfigureAwait(false);
            throw;
        }
    }

    private async Task CancelRemoteJobBestEffortAsync(RunningJob job, IQueuedImageProvider provider, ProviderJobHandle handle)
    {
        if (!job.TryBeginRemoteCancel())
        {
            return;
        }

        try
        {
            var outcome = await provider.CancelAsync(handle, CancellationToken.None).ConfigureAwait(false);
            logger.LogWarning(
                "Remote provider job cancellation {GenerationId} for session {SessionId} completed with outcome {Outcome}.",
                job.GenerationId,
                job.SessionId,
                outcome);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Remote provider job cancellation {GenerationId} for session {SessionId} failed.",
                job.GenerationId,
                job.SessionId);
            logger.LogDebug(ex, "Remote provider job cancellation detail {GenerationId}", job.GenerationId);
        }
    }

    private static async Task WaitForLocalJobTerminalAsync(IQueuedImageProvider provider, ProviderJobHandle handle)
    {
        while (!(await provider.PollAsync(handle, CancellationToken.None).ConfigureAwait(false)).IsTerminal)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }
    }

    /// <summary>Follows the saved job until it ends; every change is saved and announced.</summary>
    private async Task WatchQueuedAsync(Generation generation, IQueuedImageProvider queued, RunningJob job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProviderJobHandle handle;
        try
        {
            handle = JsonSerializer.Deserialize<ProviderJobHandle>(generation.JobJson!)
                ?? throw new InvalidOperationException("The saved job handle is empty.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            await FailGenerationAsync(generation, ErrorKind.Unknown, ex.Message, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Only the reading of the next update is a polling failure. The handling of an update — the file store, the
        // persistence — is not: such an error must not be turned into a second terminal status of the generation.
        var isLocalAdapter = queued is InMemoryQueuedImageProvider or InMemoryQueuedImageConnection;
        var enumerator = ProviderJobPoller.WatchAsync(
            queued,
            handle,
            isLocalAdapter ? TimeSpan.FromMilliseconds(50) : PollMinInterval,
            isLocalAdapter ? TimeSpan.FromMilliseconds(250) : PollMaxInterval,
            MaxPollNetworkFailures,
            cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                JobUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    await job.StateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (generation.IsFinished) return;
                        await ApplyPollingFailureAsync(generation, ex, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        job.StateGate.Release();
                    }
                    return;
                }

                update = enumerator.Current;
                await job.StateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (generation.IsFinished) return;
                    await ApplyUpdateAsync(generation, update, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    job.StateGate.Release();
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Ends the generation after a failure of the polling itself (C1.6, S3.8).</summary>
    private async Task ApplyPollingFailureAsync(Generation generation, Exception failure, CancellationToken cancellationToken)
    {
        var kind = failure is ProviderException provider ? provider.Kind : ErrorKind.Unknown;
        if (kind == ErrorKind.Moderated)
        {
            generation.Status = GenerationStatus.Filtered;
        }
        else
        {
            generation.Status = GenerationStatus.Failed;
        }

        generation.ErrorKind = kind;
        generation.ErrorMessage = failure.Message;
        generation.CompletedAt = timeProvider.GetUtcNow();
        LogPollingFailure(generation, kind, failure);
        await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The operational entry carries the kind, not the text of the provider; the detail goes to Debug only.</summary>
    private void LogPollingFailure(Generation generation, ErrorKind kind, Exception failure)
    {
        logger.LogWarning(
            "Polling of generation {GenerationId} of provider {ProviderId} failed with {ErrorKind} in status {Status}.",
            generation.Id,
            generation.ProviderId,
            kind,
            generation.Status);
        logger.LogDebug(
            failure,
            "Details of the polling failure of generation {GenerationId}: {Failure}",
            generation.Id,
            failure.Message);
    }

    /// <summary>Ends the generation with the given kind and message.</summary>
    private async Task FailGenerationAsync(Generation generation, ErrorKind kind, string message, CancellationToken cancellationToken)
    {
        generation.Status = GenerationStatus.Failed;
        generation.ErrorKind = kind;
        generation.ErrorMessage = message;
        generation.CompletedAt = timeProvider.GetUtcNow();
        await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyUpdateAsync(Generation generation, JobUpdate update, CancellationToken cancellationToken)
    {
        if ((update.State == JobState.Queued && generation.Status == GenerationStatus.Queued)
            || (update.State == JobState.Running && generation.Status == GenerationStatus.Running)
            || (update.State == JobState.Queued && generation.Status == GenerationStatus.Running))
        {
            return;
        }

        switch (update.State)
        {
            case JobState.Queued:
                generation.Status = GenerationStatus.Queued;
                break;
            case JobState.Running:
                generation.Status = GenerationStatus.Running;
                break;
            case JobState.Succeeded:
                await SaveResultAndPersistAsync(generation, update.Result!, cancellationToken).ConfigureAwait(false);
                return;
            case JobState.Filtered:
                generation.Status = GenerationStatus.Filtered;
                generation.ErrorKind = ErrorKind.Moderated;
                generation.ErrorMessage = update.ErrorMessage;
                generation.CompletedAt = timeProvider.GetUtcNow();
                break;
            case JobState.Failed:
                generation.Status = GenerationStatus.Failed;
                generation.ErrorKind = update.ErrorKind ?? ErrorKind.Unknown;
                generation.ErrorMessage = update.ErrorMessage;
                generation.CompletedAt = timeProvider.GetUtcNow();
                break;
            case JobState.Cancelled:
                generation.Status = GenerationStatus.Cancelled;
                generation.CompletedAt = timeProvider.GetUtcNow();
                break;
        }

        await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The request of a generation with its references read; null when a reference is missing (the generation is then Failed).</summary>
    private async Task<ImageGenerationRequest?> BuildRequestAsync(Generation generation, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> references;
        try
        {
            references = await ReadReferencesAsync(generation, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Reference image of generation {GenerationId} is missing or unreadable.", generation.Id);
            generation.Status = GenerationStatus.Failed;
            generation.ErrorKind = ErrorKind.ReferenceUnreadable;
            generation.ErrorMessage = ex.Message;
            return null;
        }

        // Only the values the plan accepted are sent: a dropped parameter is not in the request.
        var sent = generation.Outcomes
            .Where(o => o.Sent is not null)
            .ToDictionary(o => o.Name, o => o.Sent!, StringComparer.Ordinal);
        return new ImageGenerationRequest(
            generation.ModelId,
            generation.Prompt,
            generation.SeedApplied ? generation.Seed : null,
            sent.GetValueOrDefault(GenerationParameterPlanner.AspectRatioName),
            sent.GetValueOrDefault(GenerationParameterPlanner.ResolutionName),
            sent.GetValueOrDefault(GenerationParameterPlanner.OutputFormatName),
            sent.GetValueOrDefault(GenerationParameterPlanner.QualityName),
            references);
    }

    /// <summary>
    /// Logs a state change of one generation with its id. Filtered is a normal result of a provider's moderation,
    /// so only Failed is logged as a warning. The prompt is not logged here (it is user data).
    /// </summary>
    private void LogStatus(Generation generation)
    {
        const string Template =
            "Generation {GenerationId} is {Status} (run {RunId}, model {ProviderId}/{ModelId}, duration {DurationMs} ms, error {ErrorKind})";
        var durationMs = generation.CompletedAt is { } completed
            ? (long)(completed - generation.CreatedAt).TotalMilliseconds
            : (long?)null;
        if (generation.Status == GenerationStatus.Failed)
        {
            logger.LogWarning(Template, generation.Id, generation.Status, generation.RunId, generation.ProviderId, generation.ModelId, durationMs, generation.ErrorKind);
        }
        else
        {
            logger.LogInformation(Template, generation.Id, generation.Status, generation.RunId, generation.ProviderId, generation.ModelId, durationMs, generation.ErrorKind);
        }
    }

    private async Task<IReadOnlyList<string>> ReadReferencesAsync(Generation generation, CancellationToken cancellationToken)
    {
        var urls = new List<string>(generation.ReferenceAttachmentIds.Count);
        foreach (var attachmentId in generation.ReferenceAttachmentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            urls.Add(await attachments.ReadDataUrlAsync(attachmentId, cancellationToken));
        }

        return urls;
    }

    /// <summary>Saves the generation, then logs and raises the event only after the repository confirms the update.</summary>
    private async Task SaveAndNotifyAsync(Generation generation, CancellationToken cancellationToken = default)
    {
        await PersistStateWithRetryAsync(generation, cancellationToken).ConfigureAwait(false);
        NotifyConfirmedState(generation);
    }

    private async Task SaveResultAndPersistAsync(
        Generation generation,
        ImageGenerationResult result,
        CancellationToken cancellationToken)
    {
        string relativePath;
        try
        {
            relativePath = await files.SaveAsync(
                generation.SessionId, generation.Id, result.Content, result.MediaType, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            generation.CostUsd = result.CostUsd;
            await FailGenerationAsync(
                generation,
                ErrorKind.ImageSaveFailed,
                "Could not save the generated image. Check free space and folder permissions.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        generation.ImagePath = relativePath;
        generation.MediaType = result.MediaType;
        generation.CostUsd = result.CostUsd;
        generation.Status = GenerationStatus.Succeeded;
        generation.CompletedAt = timeProvider.GetUtcNow();

        try
        {
            await PersistStateWithRetryAsync(generation, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await files.DeleteFileAsync(relativePath, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                logger.LogWarning(
                    "Could not clean up image file for generation {GenerationId} in session {SessionId}.",
                    generation.Id,
                    generation.SessionId);
                logger.LogDebug(
                    cleanupException,
                    "Image file cleanup detail for generation {GenerationId}: {RelativePath}",
                    generation.Id,
                    relativePath);
            }
            finally
            {
                generation.ImagePath = null;
                generation.MediaType = null;
            }

            throw;
        }

        NotifyConfirmedState(generation);
    }

    private void NotifyConfirmedState(Generation generation)
    {
        LogStatus(generation);
        PublishConfirmedState(generation);
    }

    private void PublishConfirmedState(Generation generation)
    {
        try
        {
            GenerationUpdated?.Invoke(this, new GenerationUpdatedEventArgs(Snapshot(generation)));
        }
        catch (Exception ex)
        {
            logger.LogWarning("A handler of GenerationUpdated failed for generation {GenerationId} with status {Status}.", generation.Id, generation.Status);
            logger.LogDebug(ex, "GenerationUpdated handler failure detail {GenerationId}", generation.Id);
        }
    }

    private async Task PersistStateWithRetryAsync(Generation generation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await runs.UpdateGenerationAsync(generation, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (StorageWriteException ex) when (ex.IsTransient && attempt < StateSaveMaxAttempts)
            {
                logger.LogWarning(
                    "Retrying generation state write {GenerationId} in session {SessionId} for provider {ProviderId} after attempt {Attempt}.",
                    generation.Id,
                    generation.SessionId,
                    generation.ProviderId,
                    attempt);
                logger.LogDebug(ex, "Transient generation state write detail {GenerationId} attempt {Attempt}", generation.Id, attempt);
                await _delay.DelayAsync(StateSaveRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class SessionJobState(Guid sessionId)
    {
        public Guid SessionId { get; } = sessionId;
        public bool IsDeleting { get; set; }
        public int StartLeaseCount { get; set; }
        public TaskCompletionSource StartsDrained { get; set; } = CompletedSignal();
        public CancellationTokenSource StopSource { get; } = new();
        public TaskCompletionSource? StopCompletion { get; set; }
        private static TaskCompletionSource CompletedSignal()
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            signal.SetResult();
            return signal;
        }
    }

    private sealed class StartLease(GenerationService owner, SessionJobState state) : IDisposable
    {
        private GenerationService? _owner = owner;
        public SessionJobState State { get; } = state;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleaseStartLease(State);
    }

    private sealed class RunningJob
    {
        private readonly object _remoteJobGate = new();
        private readonly object _userCancelGate = new();
        private IQueuedImageProvider? _queuedProvider;
        private ProviderJobHandle? _submittedHandle;
        private int _remoteCancelStarted;
        private int _phase;
        private bool _userCancelRequested;
        private Task<GenerationCancelResult>? _userCancelTask;

        public RunningJob(Generation generation, CancellationTokenSource source)
        {
            Generation = generation;
            GenerationId = generation.Id;
            RunId = generation.RunId;
            SessionId = generation.SessionId;
            Source = source;
        }

        public Guid GenerationId { get; }
        public Guid RunId { get; }
        public Guid SessionId { get; }
        public Generation Generation { get; }
        public CancellationTokenSource Source { get; }
        public Task Task { get; set; } = System.Threading.Tasks.Task.CompletedTask;
        public TaskCompletionSource SubmitSettled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SemaphoreSlim StateGate { get; } = new(1, 1);

        public Task<GenerationCancelResult> GetOrStartUserCancel(Func<Task<GenerationCancelResult>> start)
        {
            lock (_userCancelGate)
            {
                return _userCancelTask ??= start();
            }
        }

        public bool IsUserCancelRequested
        {
            get
            {
                lock (_userCancelGate)
                {
                    return _userCancelRequested;
                }
            }
        }

        public void MarkPreparing()
        {
            lock (_userCancelGate)
            {
                if (_phase == 0) _phase = 1;
            }
        }

        public bool TryBeginSubmission()
        {
            lock (_userCancelGate)
            {
                if (_userCancelRequested) return false;
                _phase = 2;
                return true;
            }
        }

        public bool TryCancelBeforeSubmission()
        {
            lock (_userCancelGate)
            {
                if (_phase >= 2)
                {
                    return false;
                }

                _userCancelRequested = true;
                return true;
            }
        }

        public void PublishHandle(IQueuedImageProvider provider, ProviderJobHandle handle)
        {
            lock (_userCancelGate)
            {
                _phase = 3;
            }

            lock (_remoteJobGate)
            {
                _queuedProvider = provider;
                _submittedHandle = handle;
            }
        }

        public (IQueuedImageProvider Provider, ProviderJobHandle Handle)? GetRemoteJob()
        {
            lock (_remoteJobGate)
            {
                return _queuedProvider is not null && _submittedHandle is not null
                    ? (_queuedProvider, _submittedHandle)
                    : null;
            }
        }

        public void CompleteSubmitPhase() => SubmitSettled.TrySetResult();

        public bool TryBeginRemoteCancel() => Interlocked.CompareExchange(ref _remoteCancelStarted, 1, 0) == 0;
    }

    private static Generation Snapshot(Generation g) => new()
    {
        Id = g.Id,
        RunId = g.RunId,
        SessionId = g.SessionId,
        ProviderId = g.ProviderId,
        ModelId = g.ModelId,
        Prompt = g.Prompt,
        Seed = g.Seed,
        SeedApplied = g.SeedApplied,
        Status = g.Status,
        ImagePath = g.ImagePath,
        MediaType = g.MediaType,
        Kind = g.Kind,
        DurationSeconds = g.DurationSeconds,
        CostUsd = g.CostUsd,
        ErrorKind = g.ErrorKind,
        ErrorMessage = g.ErrorMessage,
        CreatedAt = g.CreatedAt,
        CompletedAt = g.CompletedAt,
        SortOrder = g.SortOrder,
        Outcomes = g.Outcomes,
        ReferenceAttachmentIds = g.ReferenceAttachmentIds,
        CancelRequestedAt = g.CancelRequestedAt,
    };
}
