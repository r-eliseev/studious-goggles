using System.Collections.Concurrent;
using ImageGen.Core.AnthropicCompatible;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.OpenAiCompatible;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Providers;

/// <summary>Creates one client per configured provider (on first use) and picks it by the provider's kind.</summary>
public sealed class ProviderRouter(
    ProviderRegistry registry,
    IHttpClientFactory httpClientFactory,
    ISecretStore secretStore,
    TimeProvider timeProvider,
    ILoggerFactory? loggerFactory = null) : IProviderRouter
{
    private readonly ConcurrentDictionary<string, IProviderConnection> _connections = new(StringComparer.Ordinal);

    public IReadOnlyList<string> ImageProviderIds => registry.All
        .Where(p => p.Kind != ProviderKind.AnthropicCompatible)
        .Select(p => p.Id)
        .ToList();

    public IReadOnlyList<string> ChatProviderIds => registry.All
        .Where(p => p.Kind is ProviderKind.OpenRouter or ProviderKind.OpenAiCompatible or ProviderKind.AnthropicCompatible)
        .Select(p => p.Id)
        .ToList();

    public IProviderConnection GetConnection(string providerId) =>
        _connections.GetOrAdd(providerId, id => Create(registry.Get(id)));

    public IModelProvider GetImageProvider(string providerId) =>
        GetConnection(providerId) as IModelProvider
        ?? throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not generate images.");

    public IQueuedImageProvider GetQueuedImageProvider(string providerId) =>
        GetConnection(providerId) as IQueuedImageProvider
        ?? throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not support queued image generation.");

    public IAssistantChatClient GetChatClient(string providerId) =>
        GetConnection(providerId) as IAssistantChatClient
        ?? throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not support assistant chat.");

    public ITextEmbeddingClient GetTextEmbeddingClient(string providerId) =>
        GetConnection(providerId) as ITextEmbeddingClient
        ?? throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not support text embeddings.");

    public IVideoModelProvider GetVideoModelProvider(string providerId)
    {
        if (registry.Get(providerId).Kind != ProviderKind.OpenRouter)
        {
            throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not support video models.");
        }

        return GetConnection(providerId) as IVideoModelProvider
            ?? throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not support video models.");
    }

    public IQueuedVideoProvider GetQueuedVideoProvider(string providerId)
    {
        if (registry.Get(providerId).Kind != ProviderKind.OpenRouter)
        {
            throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not support queued video generation.");
        }

        return GetConnection(providerId) as IQueuedVideoProvider
            ?? throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' does not support queued video generation.");
    }

    private IProviderConnection Create(ProviderConfig config) => config.Kind switch
    {
        ProviderKind.OpenRouter => Wrap(new OpenRouterClient(config, httpClientFactory, secretStore, timeProvider, loggerFactory?.CreateLogger<OpenRouterClient>()), config.Id),
        ProviderKind.OpenAiCompatible => Wrap(new OpenAiCompatibleClient(config, httpClientFactory, secretStore, timeProvider), config.Id),
        ProviderKind.AnthropicCompatible => new AnthropicCompatibleClient(config, httpClientFactory, secretStore, timeProvider),
        ProviderKind.Fal => new ImageGen.Core.Fal.FalImageProvider(config, httpClientFactory, secretStore, timeProvider),
        _ => throw new InvalidOperationException($"Unknown provider kind {config.Kind}."),
    };

    private InMemoryQueuedImageConnection Wrap(IProviderConnection provider, string providerId) =>
        new((IModelProvider)provider, (IAssistantChatClient)provider, (ISynchronousImageTransport)provider, providerId, timeProvider);
}
