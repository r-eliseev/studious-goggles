using ImageGen.Presentation.PageModels;

namespace ImageGen.Presentation.Tests.PageModels;

public sealed class CardImageLayoutTests
{
    [Theory]
    [InlineData(1024, 1024, 264d)]
    [InlineData(16, 9, 148.5d)]
    [InlineData(9, 16, 352d)]
    [InlineData(3, 2, 176d)]
    [InlineData(null, null, 176d)]
    [InlineData(1, 1000, 352d)]
    [InlineData(1000, 1, 132d)]
    public void HeightFor_ReturnsBoundedAspectFitHeight(int? pixelWidth, int? pixelHeight, double expected)
    {
        Assert.Equal(expected, CardImageLayout.HeightFor(pixelWidth, pixelHeight));
    }
}
