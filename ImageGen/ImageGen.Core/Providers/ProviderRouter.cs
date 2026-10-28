using System.Collections.Concurrent;
using ImageGen.Core.AnthropicCompatible;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.OpenAiCompatible;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;

namespace ImageGen.Core.Providers;

/// <summary>Creates one client per configured provider (on first use) and picks it by the provider's kind.</summary>
public sealed class ProviderRouter(
    ProviderRegistry registry,
    IHttpClientFactory httpClientFactory,
    ISecretStore secretStore,
    TimeProvider timeProvider) : IProviderRouter
{
    private readonly ConcurrentDictionary<string, IProviderConnection> _connections = new(StringComparer.Ordinal);

    public IReadOnlyList<string> ImageProviderIds => registry.All
        .Where(p => p.Kind != ProviderKind.AnthropicCompatible)
        .Select(p => p.Id)
        .ToList();

    public IProviderConnection GetConnection(string providerId) =>
        _connections.GetOrAdd(providerId, id => Create(registry.Get(id)));

    public IModelProvider GetImageProvider(string providerId) =>
        GetConnection(providerId) as IModelProvider
        ?? throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not generate images.");

    public IAssistantChatClient GetChatClient(string providerId) =>
        GetConnection(providerId) as IAssistantChatClient
        ?? throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not support assistant chat.");

    private IProviderConnection Create(ProviderConfig config) => config.Kind switch
    {
        ProviderKind.OpenRouter => new OpenRouterClient(config, httpClientFactory, secretStore, timeProvider),
        ProviderKind.OpenAiCompatible => new OpenAiCompatibleClient(config, httpClientFactory, secretStore, timeProvider),
        ProviderKind.AnthropicCompatible => new AnthropicCompatibleClient(config, httpClientFactory, secretStore, timeProvider),
        _ => throw new InvalidOperationException($"Unknown provider kind {config.Kind}."),
    };
}
