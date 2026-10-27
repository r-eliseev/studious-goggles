using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Storage;

public interface IRunRepository
{
    /// <summary>Saves the run and its generations in one transaction.</summary>
    Task AddRunAsync(Run run, IReadOnlyList<Generation> generations, CancellationToken cancellationToken);

    Task<Run?> GetRunAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Runs of the session ordered by <see cref="Run.CreatedAt"/> ascending.</summary>
    Task<IReadOnlyList<Run>> ListRunsBySessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<Generation?> GetGenerationAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Generations of the session ordered by run creation time, then <see cref="Generation.SortOrder"/>.</summary>
    Task<IReadOnlyList<Generation>> ListGenerationsBySessionAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Overwrites all mutable fields of the generation.</summary>
    Task UpdateGenerationAsync(Generation generation, CancellationToken cancellationToken);

    /// <summary>Marks every Queued or Running generation as Failed with <see cref="ErrorKind.Interrupted"/>.</summary>
    /// <returns>Number of updated generations.</returns>
    Task<int> MarkUnfinishedAsInterruptedAsync(DateTimeOffset completedAt, CancellationToken cancellationToken);
}
