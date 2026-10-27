namespace ImageGen.Core.Providers;

public interface IImageGenerator
{
    /// <summary>Generates one image. Throws <see cref="ProviderException"/> on provider errors.</summary>
    Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken);
}
