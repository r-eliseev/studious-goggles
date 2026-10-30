namespace ImageGen.Presentation.Services;

public readonly record struct ImageViewerPoint(double X, double Y);

public readonly record struct ImageViewerRect(double X, double Y, double Width, double Height)
{
    public bool Contains(ImageViewerPoint point) =>
        point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
}

/// <summary>Geometry-only hit testing for a centered AspectFit image viewer.</summary>
public static class ImageViewerHitTest
{
    public static ImageViewerRect? GetAspectFitRect(ImageViewerRect viewport, int pixelWidth, int pixelHeight)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0 || pixelWidth <= 0 || pixelHeight <= 0) return null;
        var scale = Math.Min(viewport.Width / pixelWidth, viewport.Height / pixelHeight);
        var width = pixelWidth * scale;
        var height = pixelHeight * scale;
        return new ImageViewerRect(viewport.X + (viewport.Width - width) / 2, viewport.Y + (viewport.Height - height) / 2, width, height);
    }

    public static bool IsBackgroundTap(
        ImageViewerPoint? point,
        ImageViewerRect viewport,
        ImageViewerRect? visibleImage,
        IReadOnlyList<ImageViewerRect> protectedControls,
        bool imageGeometryKnown)
    {
        if (point is not { } tap) return false;
        if (protectedControls.Any(control => control.Contains(tap))) return false;
        if (!viewport.Contains(tap)) return true;
        if (!imageGeometryKnown || visibleImage is null) return false;
        return !visibleImage.Value.Contains(tap);
    }
}
