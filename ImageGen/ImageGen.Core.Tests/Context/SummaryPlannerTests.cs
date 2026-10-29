using ImageGen.Core.Context;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Context;

public class SummaryPlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    // 2000 characters are about 580 estimated tokens.
    private static readonly string LongText = new('x', 2000);

    private static List<ChatMessage> History(int count, Func<int, bool>? isError = null) =>
        Enumerable.Range(0, count).Select(i => new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.Empty,
            Role = i % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
            Text = LongText,
            CreatedAt = Now.AddMinutes(i),
            IsError = isError?.Invoke(i) ?? false,
        }).ToList();

    private static ConversationSummary SummaryUpTo(Guid messageId) => new()
    {
        SessionId = Guid.Empty,
        Text = "earlier",
        CoversUpToMessageId = messageId,
        Model = new ModelRef(ProviderIds.OpenRouter, "vendor/model"),
        CreatedAt = Now,
    };

    [Fact]
    public void Plan_ShortHistory_Null()
    {
        var messages = History(30).Select(m => { m.Text = new string('x', 1000); return m; }).ToList();

        Assert.Null(SummaryPlanner.Plan(messages, null, historyBudget: 20_000, factor: 1.0));
    }

    [Fact]
    public void Plan_OverEightyPercent_SummarizesAllButLast20()
    {
        var messages = History(60);

        var plan = SummaryPlanner.Plan(messages, null, historyBudget: 20_000, factor: 1.0);

        Assert.NotNull(plan);
        Assert.Equal(40, plan.ToSummarize.Count);
        Assert.Equal(messages[0].Id, plan.ToSummarize[0].Id);
        Assert.Equal(messages[39].Id, plan.CoversUpToMessageId);
    }

    [Fact]
    public void Plan_FewerThan10Old_Null()
    {
        var messages = History(25);

        Assert.Null(SummaryPlanner.Plan(messages, null, historyBudget: 10_000, factor: 1.0));
    }

    [Fact]
    public void Plan_ExistingSummary_StartsAfterCoverage()
    {
        var messages = History(60);
        var current = SummaryUpTo(messages[9].Id);

        var plan = SummaryPlanner.Plan(messages, current, historyBudget: 20_000, factor: 1.0);

        Assert.NotNull(plan);
        Assert.Equal(30, plan.ToSummarize.Count);
        Assert.Equal(messages[10].Id, plan.ToSummarize[0].Id);
        Assert.Equal(messages[39].Id, plan.CoversUpToMessageId);
    }

    [Fact]
    public void Plan_SkipsErrors()
    {
        var messages = History(60, isError: i => i is 5 or 6);

        var plan = SummaryPlanner.Plan(messages, null, historyBudget: 20_000, factor: 1.0);

        Assert.NotNull(plan);
        Assert.DoesNotContain(plan.ToSummarize, m => m.IsError);
    }
}
