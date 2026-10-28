using ImageGen.Core.Providers;

namespace ImageGen.Core.Domain.Keys;

public static class SecretNames
{
    /// <summary>Name of the OpenRouter key before provider ids existed. Kept so a saved key still works.</summary>
    public const string OpenRouterApiKey = "openrouter_api_key";

    /// <summary>Secret name of a provider's key: the old name for OpenRouter, <c>provider.&lt;id&gt;.api_key</c> for others.</summary>
    public static string ForProvider(string providerId) => providerId == ProviderIds.OpenRouter
        ? OpenRouterApiKey
        : $"provider.{providerId}.api_key";
}
