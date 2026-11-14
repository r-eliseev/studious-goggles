using System.Globalization;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class SpendingSteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    private SpendingPageModel Spending => Assert.IsType<SpendingPageModel>(
        Assert.IsType<SpendingView>(World.Window.PageArea.Content).DataContext);

    [Given("the spending clock is at {string} in time zone {string}")]
    public void GivenSpendingClockIsAtInTimeZone(string dateTime, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        World.LocalTimeZone = zone;
        World.Time.SetLocalTimeZone(zone);
        World.Time.SetUtcNow(DateTimeOffset.Parse(dateTime, CultureInfo.InvariantCulture));
    }

    [When("the spending clock advances {int} second")]
    public void WhenSpendingClockAdvances(int seconds) => World.Time.Advance(TimeSpan.FromSeconds(seconds));

    [When("the user opens the spending screen for the current day")]
    public Task WhenUserOpensSpendingForCurrentDay() => OpenSpendingAsync(SpendingPeriod.Day);

    [When("the user opens the spending screen for the current week")]
    public Task WhenUserOpensSpendingForCurrentWeek() => OpenSpendingAsync(SpendingPeriod.Week);

    [When("the user opens the spending screen for the current month")]
    public Task WhenUserOpensSpendingForCurrentMonth() => OpenSpendingAsync(SpendingPeriod.Month);

    [When("the user selects the spending month")]
    public Task WhenUserSelectsSpendingMonth() => HeadlessApp.RunAsync(async _ =>
    {
        Spending.SelectedPeriod = SpendingPeriod.Month;
        await Spending.RefreshCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user saves a monthly spending limit of {string}")]
    public Task WhenUserSavesMonthlySpendingLimit(string amount) => HeadlessApp.RunAsync(async _ =>
    {
        Spending.MonthlyLimitText = amount;
        await Spending.SaveSettingsCommand.ExecuteAsync(null);
        Assert.Empty(Spending.ErrorText);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the monthly spending limit is {string}")]
    public void ThenMonthlySpendingLimitIs(string amount) => Assert.Equal(amount, Spending.MonthlyLimitText);

    private Task OpenSpendingAsync(SpendingPeriod period) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var sessions = World.Provider.GetRequiredService<SessionsPageModel>();
        await sessions.OpenSettingsCommand.ExecuteAsync(null);
        var settings = World.Provider.GetRequiredService<SettingsPageModel>();
        await World.WaitUntilAsync(() => World.Provider.GetRequiredService<AvaloniaNavigationService>().CurrentRoute == Routes.Settings,
            "the settings screen", cancellationToken);
        await settings.OpenSpendingCommand.ExecuteAsync(null);
        await World.WaitUntilAsync(() => World.Provider.GetRequiredService<AvaloniaNavigationService>().CurrentRoute == Routes.Spending,
            "the spending screen", cancellationToken);
        var page = Spending;
        page.SelectedPeriod = period;
        await page.RefreshCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("spending total is {string}")]
    public void ThenSpendingTotalIs(string amount) => Assert.Equal(amount, Spending.TotalText);

    [Then("spending has these provider and model totals:")]
    public void ThenSpendingHasProviderAndModelTotals(DataTable table)
    {
        var expected = table.Rows.Select(row => new
        {
            Provider = row["provider"],
            Model = row["model"],
            Total = decimal.Parse(row["total"], System.Globalization.CultureInfo.InvariantCulture),
            Operations = int.Parse(row["operations"], System.Globalization.CultureInfo.InvariantCulture),
        }).ToArray();
        Assert.Equal(expected.Length, Spending.Groups.Count);
        foreach (var item in expected)
        {
            var group = Assert.Single(Spending.Groups, value => value.ProviderId == item.Provider && value.ModelId == item.Model);
            Assert.Equal(item.Total, group.TotalUsd);
            Assert.Equal(item.Operations, group.Operations);
        }
    }

    [Then("spending has {int} unknown operations")]
    public void ThenSpendingHasUnknownOperations(int count) =>
        Assert.Contains(count.ToString(CultureInfo.CurrentCulture), Spending.UnknownOperationsText, StringComparison.Ordinal);
}
