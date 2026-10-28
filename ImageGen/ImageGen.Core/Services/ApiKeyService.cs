using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>Keys of the configured providers. Each key is kept in the secret store under its provider's name.</summary>
public sealed class ApiKeyService(IProviderRouter providers, ISecretStore secretStore, ILogger<ApiKeyService> logger)
{
    public async Task<bool> HasKeyAsync(string providerId, CancellationToken cancellationToken)
    {
        var key = await secretStore.GetAsync(SecretNames.ForProvider(providerId), cancellationToken);
        return !string.IsNullOrWhiteSpace(key);
    }

    /// <summary>Checks the key with the provider and saves it only when the provider accepts it.</summary>
    public async Task<ApiKeyStatus> SaveAsync(string providerId, string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var trimmed = apiKey.Trim();
        var status = await providers.GetConnection(providerId).CheckKeyAsync(trimmed, cancellationToken);
        logger.LogInformation("API key check for provider {ProviderId}: {Status}.", providerId, status);
        if (status == ApiKeyStatus.Valid)
        {
            await secretStore.SetAsync(SecretNames.ForProvider(providerId), trimmed, cancellationToken);
            logger.LogInformation("API key saved for provider {ProviderId}.", providerId);
        }

        return status;
    }

    public async Task RemoveAsync(string providerId, CancellationToken cancellationToken)
    {
        await secretStore.RemoveAsync(SecretNames.ForProvider(providerId), cancellationToken);
        logger.LogInformation("API key removed for provider {ProviderId}.", providerId);
    }

    /// <summary>Returns "••••" plus the last 4 characters, or null when no key is saved.</summary>
    public async Task<string?> GetMaskedKeyAsync(string providerId, CancellationToken cancellationToken)
    {
        var key = await secretStore.GetAsync(SecretNames.ForProvider(providerId), cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return "••••" + (key.Length <= 4 ? key : key[^4..]);
    }
}
