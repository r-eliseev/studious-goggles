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
    IDebugTrace? trace = null)
{
    private readonly IDebugTrace _trace = trace ?? NullDebugTrace.Instance;

    public const int MaxParallelGenerations = 4;

    /// <summary>Poll interval of a queued job: starts at the minimum and grows to the maximum (ADR 0005).</summary>
    private static readonly TimeSpan PollMinInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollMaxInterval = TimeSpan.FromSeconds(5);
    private const int MaxPollNetworkFailures = 5;

    private readonly SemaphoreSlim _slots = new(MaxParallelGenerations, MaxParallelGenerations);
    private readonly ConcurrentDictionary<Guid, Task> _running = new();

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

        var run = await CreateRunAsync(sessionId, text, RunOrigin.Assistant, pairs, session.Settings.Parameters, cancellationToken);
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
        var requested = await GetRunParametersAsync(source.RunId, cancellationToken);
        return await CreateRunAsync(source.SessionId, source.Prompt, RunOrigin.Repeat,
            [(SourceModel(source), source.Seed, source.ReferenceAttachmentIds)], requested, cancellationToken);
    }

    /// <summary>New run from one generation: same prompt and model, new random seed.</summary>
    public async Task<Run> NewSeedAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var source = await GetSourceAsync(generationId, cancellationToken);
        var requested = await GetRunParametersAsync(source.RunId, cancellationToken);
        return await CreateRunAsync(source.SessionId, source.Prompt, RunOrigin.NewSeed,
            [(SourceModel(source), seeds.NextSeed(), source.ReferenceAttachmentIds)], requested, cancellationToken);
    }

    /// <summary>New run from one generation: new prompt, same model and seed.</summary>
    public async Task<Run> EditAsync(Guid generationId, string newPrompt, CancellationToken cancellationToken)
    {
        var text = NormalizePrompt(newPrompt);
        var source = await GetSourceAsync(generationId, cancellationToken);
        var requested = await GetRunParametersAsync(source.RunId, cancellationToken);
        return await CreateRunAsync(source.SessionId, text, RunOrigin.Edit,
            [(SourceModel(source), source.Seed, source.ReferenceAttachmentIds)], requested, cancellationToken);
    }

    /// <summary>New run from one generation on a model the user picked: same prompt and seed, a different model (task 076).</summary>
    public async Task<Run> RunOnModelAsync(Guid generationId, ModelRef model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var source = await GetSourceAsync(generationId, cancellationToken);
        var requested = await GetRunParametersAsync(source.RunId, cancellationToken);
        return await CreateRunAsync(source.SessionId, source.Prompt, RunOrigin.OtherModel,
            [(model, source.Seed, source.ReferenceAttachmentIds)], requested, cancellationToken);
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
                Launch(generation, () => WatchQueuedAsync(generation, queued));
            }
        }

        return recovered + await runs.MarkUnfinishedAsInterruptedAsync(timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Waits until no background generation is running. Used by tests.</summary>
    public async Task WhenIdleAsync()
    {
        while (true)
        {
            var pending = _running.Values.ToArray();
            if (pending.Length == 0)
            {
                return;
            }

            await Task.WhenAll(pending);
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
        CancellationToken cancellationToken)
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
        logger.LogInformation(
            "Run {RunId} created in session {SessionId} ({Origin}): {GenerationCount} generations.",
            run.Id,
            sessionId,
            origin,
            generations.Count);
        foreach (var generation in generations)
        {
            LogStatus(generation);
            Launch(generation);
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

    private void Launch(Generation generation) => Launch(generation, () => ExecuteAsync(generation));

    private void Launch(Generation generation, Func<Task> work)
    {
        // The task waits on this gate so that it cannot remove its own entry before the entry is added.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = generation.Id;
        var task = Task.Run(async () =>
        {
            await gate.Task.ConfigureAwait(false);
            try
            {
                await work().ConfigureAwait(false);
            }
            finally
            {
                _running.TryRemove(id, out _);
            }
        });

        _running[id] = task;
        gate.SetResult();
    }

    private async Task ExecuteAsync(Generation generation)
    {
        if (QueuedProviderOf(generation.ProviderId) is { } queued)
        {
            await ExecuteQueuedAsync(generation, queued).ConfigureAwait(false);
            return;
        }

        await _slots.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            generation.Status = GenerationStatus.Running;
            await SaveAndNotifyAsync(generation).ConfigureAwait(false);

            await GenerateAndStoreAsync(generation).ConfigureAwait(false);
            generation.CompletedAt = timeProvider.GetUtcNow();
            await SaveAndNotifyAsync(generation).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }

    /// <summary>
    /// A queued provider (fal): the slot is held while the job is submitted; the watch of the job runs after the slot is
    /// released, because a queue can take minutes.
    /// </summary>
    private async Task ExecuteQueuedAsync(Generation generation, IQueuedImageProvider queued)
    {
        await _slots.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            generation.Status = GenerationStatus.Running;
            await SaveAndNotifyAsync(generation).ConfigureAwait(false);
            await SubmitQueuedAsync(generation, queued).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }

        if (!generation.IsFinished && generation.JobJson is not null)
        {
            await WatchQueuedAsync(generation, queued).ConfigureAwait(false);
        }
    }

    private async Task SubmitQueuedAsync(Generation generation, IQueuedImageProvider queued)
    {
        var request = await BuildRequestAsync(generation).ConfigureAwait(false);
        if (request is null)
        {
            generation.CompletedAt = timeProvider.GetUtcNow();
            await SaveAndNotifyAsync(generation).ConfigureAwait(false);
            return;
        }

        try
        {
            var handle = await queued.SubmitAsync(request, CancellationToken.None).ConfigureAwait(false);
            generation.JobJson = JsonSerializer.Serialize(handle);
            generation.Status = GenerationStatus.Queued;
        }
        catch (ProviderException ex)
        {
            generation.Status = ex.Kind == ErrorKind.Moderated ? GenerationStatus.Filtered : GenerationStatus.Failed;
            generation.ErrorKind = ex.Kind;
            generation.ErrorMessage = ex.Message;
            generation.CompletedAt = timeProvider.GetUtcNow();
        }

        await SaveAndNotifyAsync(generation).ConfigureAwait(false);
    }

    /// <summary>Follows the saved job until it ends; every change is saved and announced.</summary>
    private async Task WatchQueuedAsync(Generation generation, IQueuedImageProvider queued)
    {
        ProviderJobHandle handle;
        try
        {
            handle = JsonSerializer.Deserialize<ProviderJobHandle>(generation.JobJson!)
                ?? throw new InvalidOperationException("The saved job handle is empty.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            await FailGenerationAsync(generation, ErrorKind.Unknown, ex.Message).ConfigureAwait(false);
            return;
        }

        // Only the reading of the next update is a polling failure. The handling of an update — the file store, the
        // persistence — is not: such an error must not be turned into a second terminal status of the generation.
        var enumerator = ProviderJobPoller.WatchAsync(
            queued, handle, PollMinInterval, PollMaxInterval, MaxPollNetworkFailures, CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);
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
                    await ApplyPollingFailureAsync(generation, ex).ConfigureAwait(false);
                    return;
                }

                update = enumerator.Current;
                await ApplyUpdateAsync(generation, update).ConfigureAwait(false);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Ends the generation after a failure of the polling itself (C1.6, S3.8).</summary>
    private async Task ApplyPollingFailureAsync(Generation generation, Exception failure)
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
        await SaveAndNotifyAsync(generation).ConfigureAwait(false);
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
    private async Task FailGenerationAsync(Generation generation, ErrorKind kind, string message)
    {
        generation.Status = GenerationStatus.Failed;
        generation.ErrorKind = kind;
        generation.ErrorMessage = message;
        generation.CompletedAt = timeProvider.GetUtcNow();
        await SaveAndNotifyAsync(generation).ConfigureAwait(false);
    }

    private async Task ApplyUpdateAsync(Generation generation, JobUpdate update)
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
                    generation.SessionId, generation.Id, update.Result!.Content, update.Result.MediaType, CancellationToken.None)
                    .ConfigureAwait(false);
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

        await SaveAndNotifyAsync(generation).ConfigureAwait(false);
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

    private async Task GenerateAndStoreAsync(Generation generation)
    {
        var request = await BuildRequestAsync(generation).ConfigureAwait(false);
        if (request is null)
        {
            return;
        }

        try
        {
            var provider = providers.GetImageProvider(generation.ProviderId);
            var result = await provider.GenerateAsync(request, CancellationToken.None).ConfigureAwait(false);

            generation.ImagePath = await files.SaveAsync(
                generation.SessionId, generation.Id, result.Content, result.MediaType, CancellationToken.None)
                .ConfigureAwait(false);
            generation.MediaType = result.MediaType;
            generation.CostUsd = result.CostUsd;
            generation.Status = GenerationStatus.Succeeded;
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
    private async Task<ImageGenerationRequest?> BuildRequestAsync(Generation generation)
    {
        IReadOnlyList<string> references;
        try
        {
            references = await ReadReferencesAsync(generation);
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

    private async Task<IReadOnlyList<string>> ReadReferencesAsync(Generation generation)
    {
        var urls = new List<string>(generation.ReferenceAttachmentIds.Count);
        foreach (var attachmentId in generation.ReferenceAttachmentIds)
        {
            urls.Add(await attachments.ReadDataUrlAsync(attachmentId, CancellationToken.None));
        }

        return urls;
    }

    /// <summary>Saves the generation, then raises the event with a copy. A failed save is logged and not reported.</summary>
    private async Task SaveAndNotifyAsync(Generation generation)
    {
        LogStatus(generation);
        try
        {
            await runs.UpdateGenerationAsync(generation, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not save generation {GenerationId} with status {Status}.", generation.Id, generation.Status);
            return;
        }

        try
        {
            GenerationUpdated?.Invoke(this, new GenerationUpdatedEventArgs(Snapshot(generation)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "A handler of GenerationUpdated failed for generation {GenerationId}.", generation.Id);
        }
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
