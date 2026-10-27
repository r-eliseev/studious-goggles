namespace ImageGen.Core.OpenRouter;

/// <param name="ArgumentsJson">Tool arguments as returned by the model (a JSON string).</param>
public sealed record OpenRouterToolCall(string Name, string ArgumentsJson);
