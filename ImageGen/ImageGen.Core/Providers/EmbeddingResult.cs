namespace ImageGen.Core.Providers;

public sealed record EmbeddingResult(float[][] Vectors, string Model, int Dimensions, decimal? CostUsd);
