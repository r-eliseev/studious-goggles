using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Tests.Services;

public class ImageFileNamesTests
{
    private static readonly Guid Id = Guid.Parse("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0");

    [Fact]
    public void Suggested_Png_UsesFirst8IdCharactersAndPngExtension()
    {
        Assert.Equal("imagegen-0f1e2d3c.png", ImageFileNames.Suggested(Id, "image/png"));
    }

    [Fact]
    public void Suggested_Jpeg_UsesJpgExtension()
    {
        Assert.Equal("imagegen-0f1e2d3c.jpg", ImageFileNames.Suggested(Id, "image/jpeg"));
    }

    [Fact]
    public void Suggested_Webp_UsesWebpExtension()
    {
        Assert.Equal("imagegen-0f1e2d3c.webp", ImageFileNames.Suggested(Id, "image/webp"));
    }

    [Fact]
    public void Suggested_UnknownType_FallsBackToPng()
    {
        Assert.Equal("imagegen-0f1e2d3c.png", ImageFileNames.Suggested(Id, "application/octet-stream"));
    }
}
