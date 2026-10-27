using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Services;

/// <param name="Run">Run started by the assistant, or null.</param>
public sealed record AssistantTurnResult(ChatMessage UserMessage, ChatMessage AssistantMessage, Run? Run);
