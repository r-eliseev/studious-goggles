namespace ImageGen.Core.Providers;

public interface ISynchronousImageTransport
{
    Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken);
}
