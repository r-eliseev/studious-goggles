using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Services;

namespace ImageGen.Core.Tests.Services;

public class TimelineOrderingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Session TestSession = new()
    {
        Id = Guid.NewGuid(),
        Title = "s",
        CreatedAt = T0,
        UpdatedAt = T0,
        Settings = SessionSettings.CreateDefault(),
    };

    private static ChatMessage Message(int second, string text, Guid? runId = null) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = TestSession.Id,
        Role = ChatRole.User,
        Text = text,
        CreatedAt = T0.AddSeconds(second),
        RunId = runId,
    };

    private static Run Run(int second, string prompt) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = TestSession.Id,
        Prompt = prompt,
        Origin = RunOrigin.Assistant,
        CreatedAt = T0.AddSeconds(second),
    };

    private static Generation Generation(Run run, int sortOrder) => new()
    {
        Id = Guid.NewGuid(),
        RunId = run.Id,
        SessionId = TestSession.Id,
        ModelId = "m",
        Prompt = run.Prompt,
        Seed = sortOrder,
        SeedApplied = true,
        Status = GenerationStatus.Queued,
        CreatedAt = run.CreatedAt,
        SortOrder = sortOrder,
    };

    [Fact]
    public void Order_LinkedRun_IsPlacedRightAfterItsMessage()
    {
        var assistantRun = Run(1, "fox");
        var user = Message(0, "draw a fox");
        var assistant = Message(2, "Prompt: fox", assistantRun.Id);

        var entries = TimelineOrdering.Order(new SessionTimeline(TestSession, [user, assistant], [assistantRun], []));

        Assert.Equal(3, entries.Count);
        Assert.Same(user, entries[0].Message);
        Assert.Same(assistant, entries[1].Message);
        Assert.Same(assistantRun, entries[2].Run);
    }

    [Fact]
    public void Order_UnlinkedRun_IsPlacedByTime()
    {
        var first = Message(0, "a");
        var repeat = Run(5, "fox");
        var later = Message(10, "b");

        var entries = TimelineOrdering.Order(new SessionTimeline(TestSession, [first, later], [repeat], []));

        Assert.Same(first, entries[0].Message);
        Assert.Same(repeat, entries[1].Run);
        Assert.Same(later, entries[2].Message);
    }

    [Fact]
    public void Order_Generations_AreGroupedByRunAndSorted()
    {
        var run = Run(0, "fox");
        var g1 = Generation(run, 1);
        var g0 = Generation(run, 0);

        var entries = TimelineOrdering.Order(new SessionTimeline(TestSession, [], [run], [g1, g0]));

        var entry = Assert.Single(entries);
        Assert.Equal([g0.Id, g1.Id], entry.Generations.Select(g => g.Id));
    }
}
