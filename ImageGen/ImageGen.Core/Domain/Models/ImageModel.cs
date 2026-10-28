namespace ImageGen.Core.Domain.Models;

public sealed class ImageModel
{
    /// <summary>Id of the provider that offers the model (<see cref="ImageGen.Core.Providers.ProviderIds"/>).</summary>
    public required string ProviderId { get; set; }

    public required string Id { get; set; }

    public required string Name { get; set; }

    public required bool SupportsSeed { get; set; }

    public required bool SupportsImageInput { get; set; }

    public required int MaxImagesPerRequest { get; set; }

    public required DateTimeOffset FetchedAt { get; set; }
}
