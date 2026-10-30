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
    IDebugTrace? trace = null) : ISessionGenerationCoordinator
{
    private readonly IDebugTrace _trace = trace ?? NullDebugTrace.Instance;

    public const int MaxParallelGenerations = 4;

    /// <summary>Poll interval of a queued job: starts at the minimum and grows to the maximum (ADR 0005).</summary>
    private static readonly TimeSpan PollMinInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollMaxInterval = TimeSpan.FromSeconds(5);
    private const int MaxPollNetworkFailures = 5;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _providerSlots = new(StringComparer.Ordinal);
    private readonly object _lifecycleGate = new();
    private readonly Dictionary<Guid, SessionJobState> _sessionStates = [];
    private readonly ConcurrentDictionary<Guid, RunningJob> _running = new();

    /// <summary>Raised on a background thread after each saved state change.</summary>
    public event EventHandler<GenerationUpdatedEventArgs>? GenerationUpdated;

    /// <summary>Starts a run of the session with its image models and <c>RunsPerModel</c> shared seeds.</summary>
    public async Task<Run> StartRunAsync(Guid sessionId, string prompt, CancellationToken cancellationToken)
    {
        var result = await StartRunWithAttachmentsAsync(sessionId, prompt, [], cancellationToken);
        return result.Run ?? throw new InvalidOperationException("No model of the session takes the requested input.");
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
                IProviderConnection connection;
                try
                {
                    connection = providers.GetConnection(generation.ProviderId);
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

                if (connection is IQueuedImageProvider queued)
                {
                    ProviderJobHandle? recoveredHandle = null;
                    try
                    {
                        recoveredHandle = JsonSerializer.Deserialize<ProviderJobHandle>(generation.JobJson!);
                    }
                    catch (JsonException)
                    {
                        // WatchQueuedAsync preserves the existing malformed-handle failure behavior.
                    }

                    Launch(generation, lease.State, (_, token) => WatchQueuedAsync(generation, queued, token), queued, recoveredHandle, submitAlreadySettled: true);
                }
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
            Exception[] unobservedFailures;
            lock (_lifecycleGate)
            {
                pending = _running.Values.Select(job => job.Task).ToArray();
                unobservedFailures = _sessionStates.Values.SelectMany(state => state.JobFailures).ToArray();
                foreach (var state in _sessionStates.Values)
                {
                    state.JobFailures.Clear();
                }
            }

            if (pending.Length == 0)
            {
                if (unobservedFailures.Length > 0)
                {
                    throw new AggregateException(unobservedFailures);
                }

                return;
            }

            try
            {
                await Task.WhenAll(pending);
            }
            catch
            {
                lock (_lifecycleGate)
                {
                    foreach (var state in _sessionStates.Values)
                    {
                        state.JobFailures.Clear();
                    }
                }

                throw;
            }

            if (unobservedFailures.Length > 0)
            {
                throw new AggregateException(unobservedFailures);
            }
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
            Exception[] unobservedFailures;
            lock (_lifecycleGate)
            {
                jobs = _running.Values.Where(job => job.SessionId == state.SessionId).ToArray();
                unobservedFailures = state.JobFailures.ToArray();
                state.JobFailures.Clear();
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

                try
                {
                    var outcome = await remoteJob.Value.Provider.CancelAsync(remoteJob.Value.Handle, CancellationToken.None).ConfigureAwait(false);
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

            await Task.WhenAll(jobs.Select(job => job.Task)).ConfigureAwait(false);
            if (unobservedFailures.Length > 0)
            {
                throw new AggregateException(unobservedFailures);
            }
            if (cancellationFailure is not null)
            {
                throw cancellationFailure;
            }

            state.StopCompletion!.TrySetResult();
        }
        catch (Exception ex)
        {
            lock (_lifecycleGate)
            {
                state.JobFailures.Clear();
            }

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
        bool submitAlreadySettled = false)
    {
        lock (_lifecycleGate)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(state.StopSource.Token);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var job = new RunningJob(generation.Id, generation.SessionId, source);
            if (queuedProvider is not null && submittedHandle is not null)
            {
                job.PublishHandle(queuedProvider, submittedHandle);
            }

            if (submitAlreadySettled)
            {
                job.CompleteSubmitPhase();
            }

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
                    await RunWithProviderSlotAsync(generation.ProviderId, job, source.Token, work).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (source.IsCancellationRequested)
                {
                    // Session stop is an expected job outcome, not a generation failure.
                }
                catch (Exception ex)
                {
                    lock (_lifecycleGate)
                    {
                        state.JobFailures.Add(ex);
                    }

                    throw;
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

    private async Task ExecuteAsync(Generation generation, RunningJob job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (QueuedProviderOf(generation.ProviderId) is { } queued)
        {
            await ExecuteQueuedAsync(generation, queued, job, cancellationToken).ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        generation.Status = GenerationStatus.Running;
        await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);

        await GenerateAndStoreAsync(generation, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        generation.CompletedAt = timeProvider.GetUtcNow();
        await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A queued provider (fal): the slot is held while the job is submitted; the watch of the job runs after the slot is
    /// released, because a queue can take minutes.
    /// </summary>
    private async Task ExecuteQueuedAsync(Generation generation, IQueuedImageProvider queued, RunningJob job, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            generation.Status = GenerationStatus.Running;
            await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
            await SubmitQueuedAsync(generation, queued, job, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Signal submit settlement even when preparation was cancelled; the provider slot belongs to the job wrapper.
            job.CompleteSubmitPhase();
        }

        if (!generation.IsFinished && generation.JobJson is not null)
        {
            await WatchQueuedAsync(generation, queued, cancellationToken).ConfigureAwait(false);
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

        try
        {
            var handle = await queued.SubmitAsync(request, cancellationToken).ConfigureAwait(false);
            job.PublishHandle(queued, handle);
            generation.JobJson = JsonSerializer.Serialize(handle);
            generation.Status = GenerationStatus.Queued;
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

        await SaveAndNotifyAsync(generation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Follows the saved job until it ends; every change is saved and announced.</summary>
    private async Task WatchQueuedAsync(Generation generation, IQueuedImageProvider queued, CancellationToken cancellationToken)
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
        var enumerator = ProviderJobPoller.WatchAsync(
            queued, handle, PollMinInterval, PollMaxInterval, MaxPollNetworkFailures, cancellationToken)
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

                    await ApplyPollingFailureAsync(generation, ex, cancellationToken).ConfigureAwait(false);
                    return;
                }

                update = enumerator.Current;
                await ApplyUpdateAsync(generation, update, cancellationToken).ConfigureAwait(false);
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
        switch (update.State)
        {
            case JobState.Queued:
                generation.Status = GenerationStatus.Queued;
                break;
            case JobState.Running:
                generation.Status = GenerationStatus.Running;
                break;
            case JobState.Succeeded:
                generation.ImagePath = await files.SaveAsync(
                    generation.SessionId, generation.Id, update.Result!.Content, update.Result.MediaType, cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                generation.MediaType = update.Result.MediaType;
                generation.Status = GenerationStatus.Succeeded;
                generation.CompletedAt = timeProvider.GetUtcNow();
                break;
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

    /// <summary>The client of a provider when it runs queued jobs; null for every other provider.</summary>
    private IQueuedImageProvider? QueuedProviderOf(string providerId)
    {
        try
        {
            return providers.GetConnection(providerId) as IQueuedImageProvider;
        }
        catch (ProviderException)
        {
            return null;
        }
    }

    private async Task GenerateAndStoreAsync(Generation generation, CancellationToken cancellationToken)
    {
        var request = await BuildRequestAsync(generation, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return;
        }

        try
        {
            var provider = providers.GetImageProvider(generation.ProviderId);
            var result = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            generation.ImagePath = await files.SaveAsync(
                generation.SessionId, generation.Id, result.Content, result.MediaType, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            generation.MediaType = result.MediaType;
            generation.CostUsd = result.CostUsd;
            generation.Status = GenerationStatus.Succeeded;
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Generation {GenerationId} failed unexpectedly.", generation.Id);
            generation.Status = GenerationStatus.Failed;
            generation.ErrorKind = ErrorKind.Unknown;
            generation.ErrorMessage = ex.Message;
        }
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

    /// <summary>Saves the generation, then raises the event with a copy. A failed save is logged and not reported.</summary>
    private async Task SaveAndNotifyAsync(Generation generation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogStatus(generation);
        try
        {
            await runs.UpdateGenerationAsync(generation, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not save generation {GenerationId} with status {Status}.", generation.Id, generation.Status);
            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            GenerationUpdated?.Invoke(this, new GenerationUpdatedEventArgs(Snapshot(generation)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "A handler of GenerationUpdated failed for generation {GenerationId}.", generation.Id);
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
        public List<Exception> JobFailures { get; } = [];

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

    private sealed class RunningJob(Guid generationId, Guid sessionId, CancellationTokenSource source)
    {
        private readonly object _remoteJobGate = new();
        private IQueuedImageProvider? _queuedProvider;
        private ProviderJobHandle? _submittedHandle;

        public Guid GenerationId { get; } = generationId;
        public Guid SessionId { get; } = sessionId;
        public CancellationTokenSource Source { get; } = source;
        public Task Task { get; set; } = System.Threading.Tasks.Task.CompletedTask;
        public TaskCompletionSource SubmitSettled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void PublishHandle(IQueuedImageProvider provider, ProviderJobHandle handle)
        {
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
        CostUsd = g.CostUsd,
        ErrorKind = g.ErrorKind,
        ErrorMessage = g.ErrorMessage,
        CreatedAt = g.CreatedAt,
        CompletedAt = g.CompletedAt,
        SortOrder = g.SortOrder,
        Outcomes = g.Outcomes,
        ReferenceAttachmentIds = g.ReferenceAttachmentIds,
    };
}
