using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Tests.Services;

public class ImageMediaTypeTests
{
    [Theory]
    [InlineData("fox.png", "image/png")]
    [InlineData("FOX.JPG", "image/jpeg")]
    [InlineData("fox.jpeg", "image/jpeg")]
    [InlineData("fox.webp", "image/webp")]
    [InlineData("fox.gif", "application/octet-stream")]
    [InlineData("notes.txt", "application/octet-stream")]
    public void FromFileName_ByExtension(string fileName, string expected)
    {
        Assert.Equal(expected, ImageMediaType.FromFileName(fileName));
    }
}
