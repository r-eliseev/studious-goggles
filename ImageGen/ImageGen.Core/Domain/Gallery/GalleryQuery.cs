using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Domain.Gallery;

public sealed record GalleryQuery(
    int PageSize,
    GalleryCursor? Cursor = null,
    ModelRef? Model = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
