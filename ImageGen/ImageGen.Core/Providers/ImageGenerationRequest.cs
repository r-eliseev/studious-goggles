namespace ImageGen.Core.Providers;

/// <param name="Seed">Null when the seed must not be sent.</param>
public sealed record ImageGenerationRequest(string ModelId, string Prompt, long? Seed);
