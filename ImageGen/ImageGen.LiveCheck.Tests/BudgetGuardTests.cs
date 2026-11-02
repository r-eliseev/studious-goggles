using ImageGen.LiveCheck;

namespace ImageGen.LiveCheck.Tests;

public sealed class BudgetGuardTests
{
    [Fact]
    public void TryReserve_ExceedsMoneyOrRequestCap_Stops()
    {
        var budget = new BudgetGuard(0.02m, 2);

        Assert.True(budget.TryReserve(0.01m));
        Assert.True(budget.TryReserve(0.01m));
        Assert.False(budget.TryReserve(0m));
        Assert.False(new BudgetGuard(0.02m, 3).TryReserve(0.03m));
    }

    [Fact]
    public void Settle_ActualCostReplacesEstimateAndBlocksFurtherRequests()
    {
        var budget = new BudgetGuard(0.02m, 3);
        Assert.True(budget.TryReserve(0.01m));

        budget.Settle(0.01m, 0.03m);

        Assert.Equal(0.03m, budget.SpentUsd);
        Assert.False(budget.TryReserve(0m));
    }
}
