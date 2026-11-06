using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Tests.Services;

public sealed class ImageViewerKeysTests
{
    [Theory]
    [InlineData(ImageViewerKey.Escape, ImageViewerKeyAction.Close)]
    [InlineData(ImageViewerKey.Left, ImageViewerKeyAction.Previous)]
    [InlineData(ImageViewerKey.Right, ImageViewerKeyAction.Next)]
    [InlineData(ImageViewerKey.Other, ImageViewerKeyAction.None)]
    public void GetAction_MapsKeyToViewerAction(ImageViewerKey key, ImageViewerKeyAction expected)
    {
        Assert.Equal(expected, ImageViewerKeys.GetAction(key));
    }
}
