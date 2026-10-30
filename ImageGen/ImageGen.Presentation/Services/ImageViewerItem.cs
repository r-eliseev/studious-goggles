namespace ImageGen.Presentation.Services;

/// <summary>An image and its accessible label in a viewer snapshot.</summary>
public sealed record ImageViewerItem(Guid Id, string AbsolutePath, string AccessibleLabel);
