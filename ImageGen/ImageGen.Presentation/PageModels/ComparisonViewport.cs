namespace ImageGen.Presentation.PageModels;

/// <summary>Aspect-fit geometry in viewport coordinates, with pan shared as fractions rather than pixels.</summary>
public static class ComparisonViewport
{
    public static ComparisonRect Fit(double viewportWidth, double viewportHeight, double imageWidth, double imageHeight,
        double zoom, double offsetX, double offsetY)
    {
        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0) throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (!double.IsFinite(viewportHeight) || viewportHeight <= 0) throw new ArgumentOutOfRangeException(nameof(viewportHeight));
        if (!double.IsFinite(imageWidth) || imageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth));
        if (!double.IsFinite(imageHeight) || imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageHeight));
        zoom = Math.Clamp(double.IsFinite(zoom) ? zoom : 1, 1, 8);
        offsetX = zoom == 1 ? 0 : Math.Clamp(double.IsFinite(offsetX) ? offsetX : 0, -0.5, 0.5);
        offsetY = zoom == 1 ? 0 : Math.Clamp(double.IsFinite(offsetY) ? offsetY : 0, -0.5, 0.5);

        var scale = Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight) * zoom;
        var width = imageWidth * scale;
        var height = imageHeight * scale;
        var x = (viewportWidth - width) / 2 - offsetX * Math.Max(0, width - viewportWidth);
        var y = (viewportHeight - height) / 2 - offsetY * Math.Max(0, height - viewportHeight);
        return new ComparisonRect(x, y, width, height);
    }
}
