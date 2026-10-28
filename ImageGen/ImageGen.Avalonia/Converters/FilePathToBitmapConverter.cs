using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace ImageGen.Avalonia.Converters;

/// <summary>
/// Turns the absolute path of a saved image into a bitmap. An empty path, a missing file (the image is not saved yet)
/// or a file that is not a picture gives null: the card shows no image instead of failing.
/// </summary>
public sealed class FilePathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || path.Length == 0 || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch (Exception)
        {
            // The decoder throws a different exception for every broken format; the card just shows no picture.
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
