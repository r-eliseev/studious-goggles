using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Providers;

/// <summary>
/// Model provider behind the app. Services depend on this interface, not on a concrete provider;
/// OpenRouter is the first implementation.
/// </summary>
public interface IModelProvider : IImageGenerator
{
    /// <summary>Checks the given key. Never throws for HTTP or network errors.</summary>
    Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken);

    /// <summary>Loads the image models that the provider offers.</summary>
    Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken);
}
