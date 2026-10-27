namespace ImageGen.Core.Domain.Models;

public sealed class ImageModel
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required bool SupportsSeed { get; set; }

    public required bool SupportsImageInput { get; set; }

    public required int MaxImagesPerRequest { get; set; }

    public required DateTimeOffset FetchedAt { get; set; }
}
