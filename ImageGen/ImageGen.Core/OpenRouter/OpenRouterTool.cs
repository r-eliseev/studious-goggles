namespace ImageGen.Core.OpenRouter;

/// <param name="ParametersJsonSchema">JSON Schema of the tool arguments as a JSON string.</param>
public sealed record OpenRouterTool(string Name, string Description, string ParametersJsonSchema);
