namespace ImageGen.Presentation.Spending;

public sealed record SpendingSettings(decimal? MonthlyLimitUsd, decimal? ExpensiveRunThresholdUsd)
{
    public static SpendingSettings Default { get; } = new(null, null);
}
