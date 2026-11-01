using System.Globalization;
using Microsoft.Maui.Devices;

namespace ImageGen.Maui.Converters;

/// <summary>
/// Gives the card "…" button its actions-sheet command everywhere except Windows. There the button opens the card's
/// context menu from code-behind (ChatPage.OnCardMoreClicked), so the sheet must not run too.
/// </summary>
public sealed class ActionsSheetCommandConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        DeviceInfo.Platform == DevicePlatform.WinUI ? null : value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
