using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Providers;

/// <summary>One reading of a job's state. Only the fields of its state are set.</summary>
public sealed record JobUpdate(
    JobState State,
    int? QueuePosition = null,
    string? Log = null,
    ImageGenerationResult? Result = null,
    ErrorKind? ErrorKind = null,
    string? ErrorMessage = null)
{
    /// <summary>A final state: the watch ends after it.</summary>
    public bool IsTerminal => State is JobState.Succeeded or JobState.Filtered or JobState.Failed or JobState.Cancelled;
}
