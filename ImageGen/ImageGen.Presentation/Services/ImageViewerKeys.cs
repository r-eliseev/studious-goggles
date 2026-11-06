namespace ImageGen.Presentation.Services;

public enum ImageViewerKey
{
    Escape,
    Left,
    Right,
    Other,
}

public enum ImageViewerKeyAction
{
    None,
    Close,
    Previous,
    Next,
}

public static class ImageViewerKeys
{
    public static ImageViewerKeyAction GetAction(ImageViewerKey key) => key switch
    {
        ImageViewerKey.Escape => ImageViewerKeyAction.Close,
        ImageViewerKey.Left => ImageViewerKeyAction.Previous,
        ImageViewerKey.Right => ImageViewerKeyAction.Next,
        _ => ImageViewerKeyAction.None,
    };
}
