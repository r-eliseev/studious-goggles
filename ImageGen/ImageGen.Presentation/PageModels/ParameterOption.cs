namespace ImageGen.Presentation.PageModels;

/// <summary>One choice of a parameter picker. <see cref="Value"/> is null for "the model's default".</summary>
public sealed record ParameterOption(string? Value, string Label);
