namespace ImageGen.Core.Providers;

/// <param name="Seed">Null when the seed must not be sent.</param>
/// <param name="AspectRatio">Only the values the plan accepted (<see cref="GenerationParameterPlanner"/>); null is not sent.</param>
public sealed record ImageGenerationRequest(
    string ModelId,
    string Prompt,
    long? Seed,
    string? AspectRatio = null,
    string? Resolution = null,
    string? OutputFormat = null,
    string? Quality = null);
