using System.Globalization;

namespace ImageGen.Maui.Converters;

/// <summary>Turns the absolute path of a saved image into an <see cref="ImageSource"/>. An empty path gives null.</summary>
public sealed class FilePathToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string path && path.Length > 0 ? ImageSource.FromFile(path) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
