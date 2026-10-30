using ImageGen.Core.Context;
using ImageGen.Core.Domain.Chat;

namespace ImageGen.Core.Tests.Context;

public class AssistantRequestBudgetTests
{
    [Fact]
    public void HistoryBudget_ExcludesReserveAndCorrectedFixedParts()
    {
        var budget = new AssistantRequestBudget(
            ContextLength: 10_000,
            FixedTokens: 100,
            ImageTokens: 50,
            SummaryTokens: 25,
            Factor: 2.0);

        // The fixed part is corrected: ceil(2.0 * (100 + 50 + 25)) = 350. The reserve is 4096 and is not corrected.
        Assert.Equal(10_000 - 4096 - 350, budget.HistoryBudget);
    }

    [Fact]
    public void CorrectedEstimate_RoundsEachSelectedMessage()
    {
        var budget = new AssistantRequestBudget(
            ContextLength: 10_000,
            FixedTokens: 0,
            ImageTokens: 0,
            SummaryTokens: 0,
            Factor: 1.1);
        var selected = new[]
        {
            new ContextMessage(ChatRole.User, "a", false),
            new ContextMessage(ChatRole.Assistant, "b", false),
        };

        // Each message is rounded on its own: ceil(5 * 1.1) = 6 each, 12 in total.
        // Rounding the sum instead would give ceil(10 * 1.1) = 11.
        Assert.Equal(4096 + 12, budget.CorrectedEstimate(selected));
    }

    [Fact]
    public void HistoryBudget_NeverNegative()
    {
        var budget = new AssistantRequestBudget(
            ContextLength: 100,
            FixedTokens: 10_000,
            ImageTokens: 0,
            SummaryTokens: 0,
            Factor: 1.0);

        Assert.Equal(0, budget.HistoryBudget);
    }
}
