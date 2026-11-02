using ImageGen.Core.Services;
using ImageGen.Core.Storage;

namespace ImageGen.Presentation.Spending;

public sealed class SpendingService(
    IExpenseRepository expenses,
    SpendingSettingsFile settingsFile,
    TimeProvider timeProvider,
    TimeZoneInfo? localTimeZone = null)
{
    private readonly TimeZoneInfo _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;

    public Task<SpendingSettings> GetSettingsAsync(CancellationToken cancellationToken) => settingsFile.ReadAsync(cancellationToken);

    public Task SaveSettingsAsync(SpendingSettings settings, CancellationToken cancellationToken) =>
        settingsFile.SaveAsync(settings, cancellationToken);

    public async Task<decimal> GetCurrentMonthTotalAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(now, _localTimeZone);
        var localStart = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var from = TimeZoneInfo.ConvertTimeToUtc(localStart, _localTimeZone);
        var entries = await expenses.ListAsync(new DateTimeOffset(from), now, cancellationToken);
        return entries.Sum(expense => expense.CostUsd);
    }

    public async Task<SpendingWarning> CheckAsync(CostEstimate estimate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        var settings = await settingsFile.ReadAsync(cancellationToken);
        if (settings.MonthlyLimitUsd is { } limit
            && await GetCurrentMonthTotalAsync(cancellationToken) + estimate.KnownUsd >= limit)
        {
            return SpendingWarning.MonthlyLimitReached;
        }

        if (settings.ExpensiveRunThresholdUsd is { } threshold && estimate.KnownUsd >= threshold)
        {
            return SpendingWarning.ExpensiveRun;
        }

        return SpendingWarning.None;
    }
}
