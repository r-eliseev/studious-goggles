using ImageGen.Core.Domain.Keys;

namespace ImageGen.Core.Providers;

/// <summary>What every provider can do with a key: check it. Implemented by image and chat clients alike.</summary>
public interface IProviderConnection
{
    /// <summary>Checks the given key. Never throws for HTTP or network errors.</summary>
    Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken);
}
