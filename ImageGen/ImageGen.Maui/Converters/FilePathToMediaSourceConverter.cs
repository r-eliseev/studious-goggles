using System.Globalization;
using CommunityToolkit.Maui.Views;

namespace ImageGen.Maui.Converters;

public sealed class FilePathToMediaSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string path && !string.IsNullOrWhiteSpace(path)
            ? MediaSource.FromFile(path)
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
