using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Platform;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation.Spending;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Tests.Spending;

public sealed class SpendingServiceTests
{
    [Fact]
    public async Task CheckAsync_ProjectedMonthTotalReachesLimit_ReturnsMonthlyLimitReached()
    {
        var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var expenses = Substitute.For<IExpenseRepository>();
        expenses.ListAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([new Expense
            {
                OperationId = "previous",
                Kind = ExpenseKind.Image,
                ProviderId = "openrouter",
                ModelId = "image-model",
                CostUsd = 4m,
                OccurredAt = now.AddDays(-1),
            }]);
        var service = await CreateServiceAsync(expenses, new SpendingSettings(5m, 1m), now);

        var warning = await service.CheckAsync(new CostEstimate(2m, [], false), TestContext.Current.CancellationToken);

        Assert.Equal(SpendingWarning.MonthlyLimitReached, warning);
    }

    [Fact]
    public async Task CheckAsync_UnderLimitButAboveExpensiveThreshold_ReturnsExpensiveRun()
    {
        var expenses = Substitute.For<IExpenseRepository>();
        expenses.ListAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Expense>>([]));
        var service = await CreateServiceAsync(expenses, new SpendingSettings(10m, 1m), DateTimeOffset.UnixEpoch);

        var warning = await service.CheckAsync(new CostEstimate(2m, [], false), TestContext.Current.CancellationToken);

        Assert.Equal(SpendingWarning.ExpensiveRun, warning);
    }

    [Fact]
    public async Task CheckAsync_NoThresholds_ReturnsNone()
    {
        var expenses = Substitute.For<IExpenseRepository>();
        expenses.ListAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Expense>>([]));
        var service = await CreateServiceAsync(expenses, SpendingSettings.Default, DateTimeOffset.UnixEpoch);

        var warning = await service.CheckAsync(new CostEstimate(25m, [new ModelRef("fal", "x")], true), TestContext.Current.CancellationToken);

        Assert.Equal(SpendingWarning.None, warning);
    }

    [Fact]
    public void SpendingSettingsFile_ParseMalformedJson_UsesDisabledDefaults()
    {
        var (settings, warning) = SpendingSettingsFile.Parse("{");

        Assert.Equal(SpendingSettings.Default, settings);
        Assert.NotNull(warning);
    }

    private static async Task<SpendingService> CreateServiceAsync(IExpenseRepository expenses, SpendingSettings settings, DateTimeOffset now)
    {
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(Path.Combine(Path.GetTempPath(), "spending-tests", Guid.NewGuid().ToString("N")));
        var settingsFile = new SpendingSettingsFile(paths, NullLogger<SpendingSettingsFile>.Instance);
        await settingsFile.SaveAsync(settings, TestContext.Current.CancellationToken);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(now);
        return new SpendingService(expenses, settingsFile, time, TimeZoneInfo.Utc);
    }
}
