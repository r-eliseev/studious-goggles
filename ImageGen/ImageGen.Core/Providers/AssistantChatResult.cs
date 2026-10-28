namespace ImageGen.Core.Providers;

/// <param name="Text">Assistant text, or null when the model returned only tool calls.</param>
/// <param name="PromptTokens">Tokens the provider counted for the request, or null when it did not say (C2.1).</param>
public sealed record AssistantChatResult(string? Text, IReadOnlyList<AssistantToolCall> ToolCalls, decimal? CostUsd, int? PromptTokens = null);
