namespace ImageGen.Core.Domain;

/// <summary>One row of the chat: either a message or a run with its generations.</summary>
public sealed record TimelineEntry(ChatMessage? Message, Run? Run, IReadOnlyList<Generation> Generations)
{
    public static TimelineEntry ForMessage(ChatMessage message) => new(message, null, []);

    public static TimelineEntry ForRun(Run run, IReadOnlyList<Generation> generations) => new(null, run, generations);
}
