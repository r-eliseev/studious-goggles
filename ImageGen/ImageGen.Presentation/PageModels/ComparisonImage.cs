namespace ImageGen.Presentation.PageModels;

public sealed record ComparisonImage(Guid GenerationId, string AbsolutePath, string ModelText, string SeedText, string CostText);
