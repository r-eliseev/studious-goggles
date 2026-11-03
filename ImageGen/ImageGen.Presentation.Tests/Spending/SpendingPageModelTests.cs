using System.Globalization;
using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Platform;
using ImageGen.Core.Storage;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Spending;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Tests.Spending;

public sealed class SpendingPageModelTests
{
    [Fact]
    public async Task RefreshAsync_MonthPeriodGroupsByProviderAndModelAndCountsUnknown()
    {
        var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var expenses = Substitute.For<IExpenseRepository>();
        expenses.ListAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([NewExpense("a", "openrouter", "fox", 1m, now), NewExpense("b", "openrouter", "fox", 2m, now), NewExpense("c", "fal", "owl", 4m, now)]);
        expenses.CountUnknownAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(2);
        var fixture = CreateModel(expenses, now);
        try
        {
            await fixture.Model.RefreshCommand.ExecuteAsync(null);

            Assert.Equal(2, fixture.Model.Groups.Count);
            Assert.Equal(3m, fixture.Model.Groups.Single(group => group.ModelId == "fox").TotalUsd);
            Assert.Equal(2, fixture.Model.Groups.Single(group => group.ModelId == "fox").Operations);
            Assert.Contains("2 operations", fixture.Model.UnknownOperationsText);
            await expenses.Received(1).ListAsync(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), now, Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(fixture.Directory)) Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_TotalUsesDollarFormatIndependentOfCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var expenses = Substitute.For<IExpenseRepository>();
        expenses.ListAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([NewExpense("a", "openrouter", "fox", 0.03m, now), NewExpense("b", "fal", "owl", 0.0045m, now)]);
        expenses.CountUnknownAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(0);
        var fixture = CreateModel(expenses, now);
        try
        {
            await fixture.Model.RefreshCommand.ExecuteAsync(null);

            Assert.Equal("$0.0345", fixture.Model.TotalText);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            if (Directory.Exists(fixture.Directory)) Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_WeekStartsOnMondayAndUsesLocalDayBoundaryAcrossDst()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var now = new DateTimeOffset(2026, 3, 8, 16, 0, 0, TimeSpan.Zero);
        var expenses = Substitute.For<IExpenseRepository>();
        expenses.ListAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Expense>>([]));
        var fixture = CreateModel(expenses, now, zone);
        try
        {
            fixture.Model.SelectedPeriod = SpendingPeriod.Week;
            await fixture.Model.RefreshCommand.ExecuteAsync(null);
            await expenses.Received(1).ListAsync(
                new DateTimeOffset(2026, 3, 2, 5, 0, 0, TimeSpan.Zero), now, Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(fixture.Directory)) Directory.Delete(fixture.Directory, recursive: true);
        }
    }

    private static Expense NewExpense(string id, string provider, string model, decimal cost, DateTimeOffset at) => new()
    {
        OperationId = id,
        Kind = ExpenseKind.Image,
        ProviderId = provider,
        ModelId = model,
        CostUsd = cost,
        OccurredAt = at,
    };

    private static (SpendingPageModel Model, string Directory) CreateModel(IExpenseRepository expenses, DateTimeOffset now, TimeZoneInfo? zone = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "spending-page-tests", Guid.NewGuid().ToString("N"));
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(directory);
        var settingsFile = new SpendingSettingsFile(paths, NullLogger<SpendingSettingsFile>.Instance);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(now);
        var service = new SpendingService(expenses, settingsFile, time, zone ?? TimeZoneInfo.Utc);
        return (new SpendingPageModel(service, expenses, time, Substitute.For<INavigationService>(), zone ?? TimeZoneInfo.Utc), directory);
    }
}
