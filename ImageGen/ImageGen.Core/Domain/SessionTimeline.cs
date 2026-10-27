namespace ImageGen.Core.Domain;

public sealed record SessionTimeline(
    Session Session,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<Run> Runs,
    IReadOnlyList<Generation> Generations);
