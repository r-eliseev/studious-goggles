namespace ImageGen.Core.Providers;

/// <summary>Provider-independent state of a queued job (ADR 0003).</summary>
public enum JobState
{
    Queued,
    Running,
    Succeeded,
    Filtered,
    Failed,
    Cancelled,
}
