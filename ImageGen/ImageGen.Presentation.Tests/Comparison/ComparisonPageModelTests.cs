using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.Tests.Comparison;

public sealed class ComparisonPageModelTests
{
    [Fact]
    public void Setters_ClampZoomOffsetAndSliderToSupportedRange()
    {
        var model = CreateModel();

        model.SetZoom(20);
        model.SetOffset(1, -1);
        model.SliderFraction = 2;

        Assert.Equal(8, model.Zoom);
        Assert.Equal(0.5, model.OffsetX);
        Assert.Equal(-0.5, model.OffsetY);
        Assert.Equal(1, model.SliderFraction);
    }

    [Fact]
    public void SetZoom_ClampsToMinimumOne()
    {
        var model = CreateModel();

        model.SetZoom(0.25);

        Assert.Equal(1, model.Zoom);
        Assert.Equal(0, model.OffsetX);
        Assert.Equal(0, model.OffsetY);
    }

    [Fact]
    public void Viewport_FitsWithoutStretchingAndCentersImage()
    {
        var fitted = ComparisonViewport.Fit(1000, 500, 400, 800, 1, 0, 0);

        Assert.Equal(375, fitted.X);
        Assert.Equal(0, fitted.Y);
        Assert.Equal(250, fitted.Width);
        Assert.Equal(500, fitted.Height);
    }

    [Fact]
    public void Viewport_AppliesSameNormalizedOffsetAtDifferentSizes()
    {
        var first = ComparisonViewport.Fit(1000, 500, 1600, 900, 2, 0.25, -0.25);
        var second = ComparisonViewport.Fit(1000, 500, 800, 450, 2, 0.25, -0.25);

        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Y, second.Y);
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
    }

    private static ComparisonPageModel CreateModel() => new(
        new ComparisonImage(Guid.NewGuid(), "/first.png", "model A", "seed 1", "$0.1"),
        new ComparisonImage(Guid.NewGuid(), "/second.png", "model B", "seed 2", AppResources.AssistantPriceUnknown));
}
