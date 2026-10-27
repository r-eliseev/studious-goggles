namespace ImageGen.Core.OpenRouter;

public sealed record OpenRouterImageResult(byte[] Content, string MediaType, decimal? CostUsd);
