using System.Collections.Concurrent;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>Creates runs and executes their generations in the background. Register as a singleton.</summary>
public sealed class GenerationService(
    ISessionRepository sessions,
    IRunRepository runs,
    IImageGenerator imageGenerator,
    IImageFileStore files,
    ModelCatalogService catalog,
    ISeedSource seedSource,
    TimeProvider timeProvider,
    ILogger<GenerationService> logger)
{
    public const int MaxParallelGenerations = 4;

    private readonly SemaphoreSlim _parallelism = new(MaxParallelGenerations, MaxParallelGenerations);
    private readonly ConcurrentDictionary<Guid, Task> _running = new();

    /// <summary>Raised on a background thread after a generation was saved with a new status.</summary>
    public event EventHandler<GenerationUpdatedEventArgs>? GenerationUpdated;

    /// <summary>Starts a run on the session models: RunsPerModel seeds shared by all models.</summary>
    public async Task<Run> StartRunAsync(Guid sessionId, string prompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        var session = await sessions.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");
        if (session.Settings.ImageModelIds.Count == 0)
        {
            throw new InvalidOperationException("No image models are selected for this session.");
        }

        var seeds = Enumerable.Range(0, session.Settings.RunsPerModel).Select(_ => seedSource.NextSeed()).ToList();
        var items = new List<(string ModelId, long Seed)>();
        foreach (var modelId in session.Settings.ImageModelIds)
        {
            foreach (var seed in seeds)
            {
                items.Add((modelId, seed));
            }
        }

        return await CreateAndStartRunAsync(sessionId, prompt.Trim(), RunOrigin.Assistant, items, cancellationToken);
    }

    /// <summary>Same prompt, model and seed as the given generation.</summary>
    public async Task<Run> RepeatAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var source = await GetGenerationOrThrowAsync(generationId, cancellationToken);
        return await CreateAndStartRunAsync(
            source.SessionId, source.Prompt, RunOrigin.Repeat, [(source.ModelId, source.Seed)], cancellationToken);
    }

    /// <summary>Same prompt and model as the given generation with a new random seed.</summary>
    public async Task<Run> NewSeedAsync(Guid generationId, CancellationToken cancellationToken)
    {
        var source = await GetGenerationOrThrowAsync(generationId, cancellationToken);
        return await CreateAndStartRunAsync(
            source.SessionId, source.Prompt, RunOrigin.NewSeed, [(source.ModelId, seedSource.NextSeed())], cancellationToken);
    }

    /// <summary>Same model and seed as the given generation with an edited prompt.</summary>
    public async Task<Run> EditAsync(Guid generationId, string newPrompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPrompt);
        var source = await GetGenerationOrThrowAsync(generationId, cancellationToken);
        return await CreateAndStartRunAsync(
            source.SessionId, newPrompt.Trim(), RunOrigin.Edit, [(source.ModelId, source.Seed)], cancellationToken);
    }

    /// <summary>Marks generations left Queued or Running by a previous app start as interrupted.</summary>
    public Task<int> RecoverInterruptedAsync(CancellationToken cancellationToken) =>
        runs.MarkUnfinishedAsInterruptedAsync(timeProvider.GetUtcNow(), cancellationToken);

    /// <summary>Completes when all generations started so far have finished. Used by tests.</summary>
    public Task WhenIdleAsync() => Task.WhenAll(_running.Values);

    private async Task<Generation> GetGenerationOrThrowAsync(Guid generationId, CancellationToken cancellationToken) =>
        await runs.GetGenerationAsync(generationId, cancellationToken)
        ?? throw new InvalidOperationException($"Generation {generationId} not found.");

    private async Task<Run> CreateAndStartRunAsync(
        Guid sessionId,
        string prompt,
        RunOrigin origin,
        IReadOnlyList<(string ModelId, long Seed)> items,
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

        var generations = new List<Generation>();
        for (var index = 0; index < items.Count; index++)
        {
            var model = await catalog.FindAsync(items[index].ModelId, cancellationToken);
            generations.Add(new Generation
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                SessionId = sessionId,
                ModelId = items[index].ModelId,
                Prompt = prompt,
                Seed = items[index].Seed,
                SeedApplied = model?.SupportsSeed ?? false,
                Status = GenerationStatus.Queued,
                CreatedAt = now,
                SortOrder = index,
            });
        }

        await runs.AddRunAsync(run, generations, cancellationToken);

        foreach (var generation in generations)
        {
            var task = Task.Run(() => ExecuteAsync(generation), CancellationToken.None);
            _running[generation.Id] = task;
            _ = task.ContinueWith(_ => _running.TryRemove(generation.Id, out var _), TaskScheduler.Default);
        }

        return run;
    }

    private async Task ExecuteAsync(Generation generation)
    {
        await _parallelism.WaitAsync();
        try
        {
            generation.Status = GenerationStatus.Running;
            await SaveAndNotifyAsync(generation);

            try
            {
                var result = await imageGenerator.GenerateAsync(
                    new ImageGenerationRequest(generation.ModelId, generation.Prompt, generation.SeedApplied ? generation.Seed : null),
                    CancellationToken.None);
                generation.ImagePath = await files.SaveAsync(
                    generation.SessionId, generation.Id, result.Content, result.MediaType, CancellationToken.None);
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
                logger.LogError(ex, "Generation {GenerationId} failed", generation.Id);
                generation.Status = GenerationStatus.Failed;
                generation.ErrorKind = ErrorKind.Unknown;
                generation.ErrorMessage = ex.Message;
            }

            generation.CompletedAt = timeProvider.GetUtcNow();
            await SaveAndNotifyAsync(generation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not save generation {GenerationId}", generation.Id);
        }
        finally
        {
            _parallelism.Release();
        }
    }

    private async Task SaveAndNotifyAsync(Generation generation)
    {
        await runs.UpdateGenerationAsync(generation, CancellationToken.None);
        GenerationUpdated?.Invoke(this, new GenerationUpdatedEventArgs(Copy(generation)));
    }

    private static Generation Copy(Generation g) => new()
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
