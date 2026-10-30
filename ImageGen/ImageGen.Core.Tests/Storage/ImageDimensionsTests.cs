using ImageGen.Core.Storage;

namespace ImageGen.Core.Tests.Storage;

public sealed class ImageDimensionsTests
{
    [Fact]
    public void Constructor_PositiveDimensions_CreatesValue()
    {
        var dimensions = new ImageDimensions(1920, 1080);

        Assert.Equal(1920, dimensions.Width);
        Assert.Equal(1080, dimensions.Height);
    }

    [Theory]
    [InlineData(0, 1, "width")]
    [InlineData(-1, 1, "width")]
    [InlineData(1, 0, "height")]
    [InlineData(1, -1, "height")]
    public void Constructor_NonPositiveDimension_Throws(int width, int height, string parameterName)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new ImageDimensions(width, height));

        Assert.Equal(parameterName, error.ParamName);
    }
}
