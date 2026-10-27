using ImageGen.Core.Domain;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;

namespace ImageGen.Core.Services;

public sealed class ApiKeyService(ISecretStore secretStore, IOpenRouterClient client)
{
    public async Task<bool> HasKeyAsync(CancellationToken cancellationToken)
    {
        var key = await secretStore.GetAsync(SecretNames.OpenRouterApiKey, cancellationToken);
        return !string.IsNullOrWhiteSpace(key);
    }

    /// <summary>Checks the key and saves it only when OpenRouter accepts it.</summary>
    public async Task<ApiKeyStatus> SaveAsync(string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var trimmed = apiKey.Trim();
        var status = await client.CheckKeyAsync(trimmed, cancellationToken);
        if (status == ApiKeyStatus.Valid)
        {
            await secretStore.SetAsync(SecretNames.OpenRouterApiKey, trimmed, cancellationToken);
        }

        return status;
    }

    public Task RemoveAsync(CancellationToken cancellationToken) =>
        secretStore.RemoveAsync(SecretNames.OpenRouterApiKey, cancellationToken);

    /// <summary>Returns "••••" plus the last 4 characters, or null when no key is saved.</summary>
    public async Task<string?> GetMaskedKeyAsync(CancellationToken cancellationToken)
    {
        var key = await secretStore.GetAsync(SecretNames.OpenRouterApiKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return "••••" + (key.Length <= 4 ? key : key[^4..]);
    }
}
