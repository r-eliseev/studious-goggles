namespace ImageGen.Core.Providers;

/// <param name="Text">Assistant text, or null when the model returned only tool calls.</param>
public sealed record AssistantChatResult(string? Text, IReadOnlyList<AssistantToolCall> ToolCalls, decimal? CostUsd);
