using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Expenses;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Spending;

namespace ImageGen.Presentation.PageModels;

public enum SpendingPeriod { Day, Week, Month }

public sealed record SpendingGroup(string ProviderId, string ModelId, decimal TotalUsd, int Operations);

/// <summary>Local-time spending overview and threshold editor.</summary>
public sealed partial class SpendingPageModel(
    SpendingService spending,
    ImageGen.Core.Storage.IExpenseRepository expenses,
    TimeProvider timeProvider,
    INavigationService navigation,
    TimeZoneInfo? localTimeZone = null) : ObservableObject
{
    private readonly TimeZoneInfo _zone = localTimeZone ?? TimeZoneInfo.Local;
    public ObservableCollection<SpendingGroup> Groups { get; } = [];
    public IReadOnlyList<SpendingPeriod> Periods { get; } = Enum.GetValues<SpendingPeriod>();

    [ObservableProperty] public partial SpendingPeriod SelectedPeriod { get; set; } = SpendingPeriod.Month;
    [ObservableProperty] public partial string TotalText { get; set; } = "";
    [ObservableProperty] public partial string UnknownOperationsText { get; set; } = "";
    [ObservableProperty] public partial string MonthlyLimitText { get; set; } = "";
    [ObservableProperty] public partial string ExpensiveRunThresholdText { get; set; } = "";
    [ObservableProperty] public partial string ErrorText { get; set; } = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        var settings = await spending.GetSettingsAsync(CancellationToken.None);
        MonthlyLimitText = FormatSetting(settings.MonthlyLimitUsd);
        ExpensiveRunThresholdText = FormatSetting(settings.ExpensiveRunThresholdUsd);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var now = timeProvider.GetUtcNow();
        var local = TimeZoneInfo.ConvertTime(now, _zone);
        var start = SelectedPeriod switch
        {
            SpendingPeriod.Day => local.Date,
            SpendingPeriod.Week => local.Date.AddDays(-(((int)local.DayOfWeek + 6) % 7)),
            _ => new DateTime(local.Year, local.Month, 1),
        };
        var end = SelectedPeriod switch
        {
            SpendingPeriod.Day => start.AddDays(1),
            SpendingPeriod.Week => start.AddDays(7),
            _ => start.AddMonths(1),
        };
        var from = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(start, DateTimeKind.Unspecified), _zone));
        var toLocal = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, _zone);
        var to = new DateTimeOffset(endUtc) < now ? new DateTimeOffset(endUtc) : now;
        var rows = await expenses.ListAsync(from, to, CancellationToken.None);
        Groups.Clear();
        foreach (var group in rows.GroupBy(row => (row.ProviderId, row.ModelId)).OrderBy(group => group.Key.ProviderId).ThenBy(group => group.Key.ModelId))
        {
            Groups.Add(new SpendingGroup(group.Key.ProviderId, group.Key.ModelId, group.Sum(item => item.CostUsd), group.Count()));
        }
        var total = rows.Sum(row => row.CostUsd).ToString("0.####", CultureInfo.InvariantCulture);
        TotalText = string.Format(CultureInfo.InvariantCulture, AppResources.CostFormat, total);
        var unknown = await expenses.CountUnknownAsync(from, to, CancellationToken.None);
        UnknownOperationsText = string.Format(CultureInfo.CurrentCulture, AppResources.SpendingUnknownOperationsFormat, unknown);
        ErrorText = "";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!TryParseSetting(MonthlyLimitText, out var monthly) || !TryParseSetting(ExpensiveRunThresholdText, out var threshold))
        {
            ErrorText = AppResources.SpendingInvalidAmount;
            return;
        }
        await spending.SaveSettingsAsync(new SpendingSettings(monthly, threshold), CancellationToken.None);
        ErrorText = "";
    }

    [RelayCommand]
    private Task BackAsync() => navigation.GoBackAsync();

    private static string FormatSetting(decimal? value) => value?.ToString(CultureInfo.CurrentCulture) ?? "";

    private static bool TryParseSetting(string value, out decimal? result)
    {
        if (string.IsNullOrWhiteSpace(value)) { result = null; return true; }
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) && amount >= 0)
        { result = amount; return true; }
        result = null;
        return false;
    }
}
