using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
namespace ImageGen.Core.Domain.Sessions;

public sealed record SessionTimeline(
    Session Session,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<Run> Runs,
    IReadOnlyList<Generation> Generations);
