namespace ImageGen.Presentation.Logging;

/// <summary>Outcome of deleting old log files while preserving the newest file.</summary>
public sealed record LogCleanupResult(int Deleted, int Failed, string? LastExceptionType = null);
