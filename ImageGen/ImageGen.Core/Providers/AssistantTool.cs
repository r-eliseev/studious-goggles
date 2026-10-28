namespace ImageGen.Core.Providers;

/// <param name="ParametersJsonSchema">JSON Schema of the tool arguments as a JSON string.</param>
public sealed record AssistantTool(string Name, string Description, string ParametersJsonSchema);
