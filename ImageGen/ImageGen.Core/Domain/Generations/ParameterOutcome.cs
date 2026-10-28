namespace ImageGen.Core.Domain.Generations;

/// <summary>
/// What happened to one requested parameter in one generation (F4.3). Stored with the generation, so the card can show
/// the replacement and the drop later.
/// </summary>
/// <param name="Name">Catalog name: aspect_ratio, resolution, output_format or quality.</param>
/// <param name="Requested">The value the user asked for.</param>
/// <param name="Sent">The value sent to the provider; null when dropped.</param>
/// <param name="Reason">Set when dropped.</param>
public sealed record ParameterOutcome(
    string Name,
    ParameterOutcomeKind Kind,
    string Requested,
    string? Sent,
    ParameterDropReason? Reason = null);
