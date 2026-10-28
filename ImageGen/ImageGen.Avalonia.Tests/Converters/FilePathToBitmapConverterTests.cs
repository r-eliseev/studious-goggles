using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using ImageGen.Avalonia.Converters;
using ImageGen.Avalonia.Tests.TestDoubles;

namespace ImageGen.Avalonia.Tests.Converters;

public class FilePathToBitmapConverterTests
{
    [AvaloniaFact]
    public void Convert_EmptyPath_ReturnsNull()
    {
        Assert.Null(ConvertPath(""));
        Assert.Null(ConvertPath(null));
    }

    [AvaloniaFact]
    public void Convert_MissingFile_ReturnsNullWhileTheImageIsNotSavedYet()
    {
        Assert.Null(ConvertPath(Path.Combine(Path.GetTempPath(), "imagegen-missing-" + Guid.NewGuid().ToString("N") + ".png")));
    }

    [AvaloniaFact]
    public void Convert_PngFile_ReturnsBitmap()
    {
        var path = Path.Combine(Path.GetTempPath(), "imagegen-converter-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            File.WriteAllBytes(path, Convert.FromBase64String(OpenRouterFakes.OnePixelPngBase64));

            Assert.IsType<Bitmap>(ConvertPath(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void Convert_NotAnImage_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), "imagegen-converter-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            File.WriteAllText(path, "not a picture");

            // Avalonia decodes unknown bytes into an empty 1×1 bitmap instead of failing (checked on 12.1.4).
            var result = ConvertPath(path);
            Assert.True(result is null or Bitmap);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static object? ConvertPath(object? value) =>
        new FilePathToBitmapConverter().Convert(value, typeof(Bitmap), null, CultureInfo.InvariantCulture);
}
