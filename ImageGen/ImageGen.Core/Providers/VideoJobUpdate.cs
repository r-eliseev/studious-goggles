using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Providers;

/// <summary>Current state of a queued video generation.</summary>
public sealed record VideoJobUpdate(JobState State, decimal? CostUsd = null, ErrorKind? ErrorKind = null, string? ErrorMessage = null);
