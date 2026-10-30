namespace ImageGen.Presentation.Services;

/// <summary>Opens a modal viewer for a fixed list of images from one generation run.</summary>
public interface IImageViewerService
{
    /// <summary>Shows the image snapshot starting at <paramref name="initialIndex"/>.</summary>
    Task ShowAsync(IReadOnlyList<ImageViewerItem> items, int initialIndex, CancellationToken cancellationToken);
}
