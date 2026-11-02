namespace ImageGen.Core.Domain.Gallery;

public sealed record GalleryPage(IReadOnlyList<GalleryItem> Items, GalleryCursor? NextCursor);
