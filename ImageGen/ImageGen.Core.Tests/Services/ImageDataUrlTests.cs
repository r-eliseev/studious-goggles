using ImageGen.Core.Services;

namespace ImageGen.Core.Tests.Services;

public class ImageDataUrlTests
{
    [Fact]
    public void From_PngBytes_ReturnsDataUrlWithMediaType()
    {
        Assert.Equal("data:image/png;base64,AQID", ImageDataUrl.From([1, 2, 3], "image/png"));
    }

    [Fact]
    public void From_JpegMediaType_UsesItInPrefix()
    {
        Assert.StartsWith("data:image/jpeg;base64,", ImageDataUrl.From([1], "image/jpeg"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void From_BlankMediaType_Throws(string mediaType)
    {
        Assert.Throws<ArgumentException>(() => ImageDataUrl.From([1], mediaType));
    }
}
