namespace ImageGen.Core.Providers;

/// <summary>Answer of a provider to a cancel request.</summary>
public enum CancelOutcome
{
    /// <summary>Removed from the queue.</summary>
    Cancelled,

    /// <summary>The job is already running; it may still finish. The watch goes on.</summary>
    CancellationRequested,

    /// <summary>The job finished before the cancel arrived; its result is the one of the watch.</summary>
    AlreadyCompleted,

    /// <summary>The provider does not know the job.</summary>
    NotFound,
}
