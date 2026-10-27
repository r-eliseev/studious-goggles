namespace ImageGen.Core.Providers;

public sealed record ImageGenerationResult(byte[] Content, string MediaType, decimal? CostUsd);
