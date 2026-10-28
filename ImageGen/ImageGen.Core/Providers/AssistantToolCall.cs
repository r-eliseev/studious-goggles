namespace ImageGen.Core.Providers;

/// <param name="ArgumentsJson">Tool arguments as returned by the model (a JSON string).</param>
public sealed record AssistantToolCall(string Name, string ArgumentsJson);
