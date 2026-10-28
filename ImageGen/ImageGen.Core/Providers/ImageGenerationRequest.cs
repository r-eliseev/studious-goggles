namespace ImageGen.Core.Providers;

/// <param name="Seed">Null when the seed must not be sent.</param>
/// <param name="ReferenceImageDataUrls">Reference images as data URLs, at most the model's limit; null or empty sends none.</param>
/// <param name="AspectRatio">Only the values the plan accepted (<see cref="GenerationParameterPlanner"/>); null is not sent.</param>
public sealed record ImageGenerationRequest(
    string ModelId,
    string Prompt,
    long? Seed,
    string? AspectRatio = null,
    string? Resolution = null,
    string? OutputFormat = null,
    string? Quality = null,
    IReadOnlyList<string>? ReferenceImageDataUrls = null);
