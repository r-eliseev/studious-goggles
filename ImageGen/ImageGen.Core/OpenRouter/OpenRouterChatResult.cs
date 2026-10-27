namespace ImageGen.Core.OpenRouter;

/// <param name="Text">Assistant text, or null when the model returned only tool calls.</param>
public sealed record OpenRouterChatResult(string? Text, IReadOnlyList<OpenRouterToolCall> ToolCalls, decimal? CostUsd);
