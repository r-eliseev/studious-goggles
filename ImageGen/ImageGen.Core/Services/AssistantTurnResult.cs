using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Services;

/// <param name="UserMessage">Saved message of the user.</param>
/// <param name="AssistantMessage">Saved reply of the assistant (text, run message or error).</param>
/// <param name="Run">Run started by the assistant tool call, or null.</param>
public sealed record AssistantTurnResult(ChatMessage UserMessage, ChatMessage AssistantMessage, Run? Run);
