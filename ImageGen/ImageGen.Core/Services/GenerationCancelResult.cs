namespace ImageGen.Core.Services;

/// <summary>Outcome of asking the provider to cancel one active generation.</summary>
public enum GenerationCancelResult
{
    NotActive,
    Cancelled,
    CancellationRequested,
    CancelFailed,
}

/// <summary>Cancellation outcome associated with the generation id in a run-level request.</summary>
public sealed record GenerationCancelItem(Guid GenerationId, GenerationCancelResult Result);
