using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Tests.Services;

public class ImageViewerHitTestTests
{
    private static readonly ImageViewerRect Viewport = new(10, 20, 200, 100);

    [Fact]
    public void IsBackgroundTap_VisibleImage_IsProtected()
    {
        var visible = ImageViewerHitTest.GetAspectFitRect(Viewport, 50, 50);

        Assert.False(ImageViewerHitTest.IsBackgroundTap(new ImageViewerPoint(110, 70), Viewport, visible, [], true));
    }

    [Theory]
    [InlineData(30, 70)]
    [InlineData(170, 70)]
    public void IsBackgroundTap_Letterbox_IsBackground(double x, double y)
    {
        var visible = ImageViewerHitTest.GetAspectFitRect(Viewport, 50, 50);

        Assert.True(ImageViewerHitTest.IsBackgroundTap(new ImageViewerPoint(x, y), Viewport, visible, [], true));
    }

    [Fact]
    public void IsBackgroundTap_ControlAndCloseButton_AreProtected()
    {
        var controls = new[] { new ImageViewerRect(20, 100, 180, 20), new ImageViewerRect(190, 20, 20, 20) };

        Assert.False(ImageViewerHitTest.IsBackgroundTap(new ImageViewerPoint(50, 110), Viewport, null, controls, false));
        Assert.False(ImageViewerHitTest.IsBackgroundTap(new ImageViewerPoint(195, 25), Viewport, null, controls, false));
    }

    [Fact]
    public void IsBackgroundTap_OuterPadding_IsBackground()
    {
        Assert.True(ImageViewerHitTest.IsBackgroundTap(new ImageViewerPoint(5, 25), Viewport, Viewport, [], true));
    }

    [Fact]
    public void IsBackgroundTap_UnknownImageGeometry_ProtectsViewportAndNullPointIsIgnored()
    {
        Assert.False(ImageViewerHitTest.IsBackgroundTap(new ImageViewerPoint(100, 70), Viewport, null, [], false));
        Assert.False(ImageViewerHitTest.IsBackgroundTap(null, Viewport, null, [], false));
    }

    [Fact]
    public void GetAspectFitRect_InvalidDimensions_ReturnsNull()
    {
        Assert.Null(ImageViewerHitTest.GetAspectFitRect(Viewport, 0, 20));
        Assert.Null(ImageViewerHitTest.GetAspectFitRect(new ImageViewerRect(0, 0, 0, 10), 20, 20));
    }
}
