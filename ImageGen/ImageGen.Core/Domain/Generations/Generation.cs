namespace ImageGen.Core.Domain.Generations;

public sealed class Generation
{
    public required Guid Id { get; set; }

    public required Guid RunId { get; set; }

    public required Guid SessionId { get; set; }

    /// <summary>Id of the provider that serves the model.</summary>
    public required string ProviderId { get; set; }

    /// <summary>Model id without the provider prefix.</summary>
    public required string ModelId { get; set; }

    /// <summary>Exact prompt sent to the model.</summary>
    public required string Prompt { get; set; }

    public required long Seed { get; set; }

    /// <summary>False when the model does not accept a seed and it was not sent.</summary>
    public required bool SeedApplied { get; set; }

    public required GenerationStatus Status { get; set; }

    /// <summary>Path relative to <c>IAppPaths.DataDirectory</c>.</summary>
    public string? ImagePath { get; set; }

    public string? MediaType { get; set; }

    public decimal? CostUsd { get; set; }

    public ErrorKind? ErrorKind { get; set; }

    public string? ErrorMessage { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Order inside the run, starting at 0.</summary>
    public required int SortOrder { get; set; }

    /// <summary>What happened to each requested parameter. Empty when none was requested or for old records.</summary>
    public IReadOnlyList<ParameterOutcome> Outcomes { get; set; } = [];

    public bool IsFinished => Status is GenerationStatus.Succeeded or GenerationStatus.Failed
        or GenerationStatus.Cancelled or GenerationStatus.Filtered;
}
