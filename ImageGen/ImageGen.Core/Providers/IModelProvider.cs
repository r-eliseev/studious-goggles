using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Providers;

/// <summary>
/// Image provider behind the app. Services depend on this interface, not on a concrete provider.
/// Implemented by the OpenRouter and OpenAI-compatible clients.
/// </summary>
public interface IModelProvider : IProviderConnection, IImageGenerator
{
    /// <summary>Loads the image models that the provider offers.</summary>
    Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken);
}
