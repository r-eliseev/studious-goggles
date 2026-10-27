using System.Collections.Concurrent;
using ImageGen.Core.Domain.Generations;
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
    IImageGenerator generator,
    IImageFileStore files,
    ModelCatalogService catalog,
    ISeedSource seeds,
    TimeProvider timeProvider,
    ILogger<GenerationService> logger)
{
    public const int MaxParallelGenerations = 4;

    private readonly SemaphoreSlim _slots = new(MaxParallelGenerations, MaxParallelGenerations);
    private readonly ConcurrentDictionary<Guid, Task> _running = new();

    /// <summary>Raised on a background thread after each saved state change.</summary>
    public event EventHandler<GenerationUpdatedEventArgs>? GenerationUpdated;

    /// <summary>Starts a run of the session with its image models and <c>RunsPerModel</c> shared seeds.</summary>
    public async Task<Run> StartRunAsync(Guid sessionId, string prompt, CancellationToken cancellationToken)
    {
        var text = NormalizePrompt(prompt);
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session.Settings.ImageModelIds.Count == 0)
        {
            throw new InvalidOperationException($"Session {sessionId} has no image models.");
        }

        var seedValues = Enumerable.Range(0, session.Settings.RunsPerModel).Select(_ => seeds.NextSeed()).ToArray();
        var pairs = new List<(string ModelId, long Seed)>();
        foreach (var modelId in session.Settings.ImageModelIds)
        {
            foreach (var seed in seedValues)
            {
                pairs.Add((modelId, seed));
            }
        }

        return await CreateRunAsync(sessionId, text, RunOrigin.Assistant, pairs, cancellationToken);
    }

    /// <summary>New run from one generation: same prompt, model and seed.</summary>
    public async Task<Run> RepeatAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var source = await GetSourceAsync(generationId, cancellationToken);
        return await CreateRunAsync(source.SessionId, source.Prompt, RunOrigin.Repeat,
            [(source.ModelId, source.Seed)], cancellationToken);
    }

    /// <summary>New run from one generation: same prompt and model, new random seed.</summary>
    public async Task<Run> NewSeedAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var source = await GetSourceAsync(generationId, cancellationToken);
        return await CreateRunAsync(source.SessionId, source.Prompt, RunOrigin.NewSeed,
            [(source.ModelId, seeds.NextSeed())], cancellationToken);
    }

    /// <summary>New run from one generation: new prompt, same model and seed.</summary>
    public async Task<Run> EditAsync(Guid generationId, string newPrompt, CancellationToken cancellationToken)
    {
        var text = NormalizePrompt(newPrompt);
        var source = await GetSourceAsync(generationId, cancellationToken);
        return await CreateRunAsync(source.SessionId, text, RunOrigin.Edit,
            [(source.ModelId, source.Seed)], cancellationToken);
    }

    /// <summary>Marks generations left unfinished by a previous app session as Failed with <see cref="ErrorKind.Interrupted"/>.</summary>
    /// <returns>Number of updated generations.</returns>
    public Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken) =>
        runs.MarkUnfinishedAsInterruptedAsync(timeProvider.GetUtcNow(), cancellationToken);

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

    private async Task<Session> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await sessions.GetAsync(sessionId, cancellationToken)
        ?? throw new InvalidOperationException($"Session {sessionId} not found.");

    private async Task<Generation> GetSourceAsync(Guid generationId, CancellationToken cancellationToken) =>
        await runs.GetGenerationAsync(generationId, cancellationToken)
        ?? throw new InvalidOperationException($"Generation {generationId} not found.");

    private async Task<Run> CreateRunAsync(
        Guid sessionId,
        string prompt,
        RunOrigin origin,
        IReadOnlyList<(string ModelId, long Seed)> pairs,
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
        };

        var seedSupport = new Dictionary<string, bool>(StringComparer.Ordinal);
        var generations = new List<Generation>(pairs.Count);
        for (var index = 0; index < pairs.Count; index++)
        {
            var (modelId, seed) = pairs[index];
            if (!seedSupport.TryGetValue(modelId, out var supportsSeed))
            {
                var model = await catalog.FindAsync(modelId, cancellationToken);
                supportsSeed = model?.SupportsSeed ?? false;
                seedSupport[modelId] = supportsSeed;
            }

            generations.Add(new Generation
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                SessionId = sessionId,
                ModelId = modelId,
                Prompt = prompt,
                Seed = seed,
                SeedApplied = supportsSeed,
                Status = GenerationStatus.Queued,
                CreatedAt = now,
                SortOrder = index,
            });
        }

        await runs.AddRunAsync(run, generations, cancellationToken);
        foreach (var generation in generations)
        {
            Launch(generation);
        }

        return run;
    }

    private void Launch(Generation generation)
    {
        // The task waits on this gate so that it cannot remove its own entry before the entry is added.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = generation.Id;
        var task = Task.Run(async () =>
        {
            await gate.Task.ConfigureAwait(false);
            try
            {
                await ExecuteAsync(generation).ConfigureAwait(false);
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

    private async Task GenerateAndStoreAsync(Generation generation)
    {
        try
        {
            var request = new ImageGenerationRequest(
                generation.ModelId,
                generation.Prompt,
                generation.SeedApplied ? generation.Seed : null);
            var result = await generator.GenerateAsync(request, CancellationToken.None).ConfigureAwait(false);

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

    /// <summary>Saves the generation, then raises the event with a copy. A failed save is logged and not reported.</summary>
    private async Task SaveAndNotifyAsync(Generation generation)
    {
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
    };
}
