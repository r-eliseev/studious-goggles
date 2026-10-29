using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Chat;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.PageModels;

/// <summary>
/// The thin line in the chat after the last message that the assistant's summary covers (C4.1). «Show summary» shows the
/// summary text with its model and price (C4.2), the same way the picture description does.
/// </summary>
public sealed partial class SummaryMarkerViewModel(ConversationSummary summary, IDialogService dialogs)
{
    public string Text => AppResources.SummaryMarker;

    public string ShowText => AppResources.ShowSummary;

    [RelayCommand]
    private Task ShowAsync()
    {
        var price = summary.CostUsd is { } cost
            ? "$" + cost.ToString("0.####", CultureInfo.InvariantCulture)
            : AppResources.DescriptionCostUnknown;
        var source = string.Format(
            CultureInfo.CurrentCulture, AppResources.DescriptionFromFormat, summary.Model.ProviderId, summary.Model.ModelId, price);
        return dialogs.AlertAsync(AppResources.SummaryTitle, summary.Text + "\n\n" + source);
    }
}
