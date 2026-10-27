namespace ImageGen.Core.OpenRouter;

/// <param name="Seed">Null when the seed must not be sent.</param>
public sealed record OpenRouterImageRequest(string Model, string Prompt, long? Seed);
