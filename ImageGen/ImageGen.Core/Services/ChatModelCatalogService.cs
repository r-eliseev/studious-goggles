using System.Collections.Concurrent;
using ImageGen.Core.Context;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>
/// Chat models of the providers, kept in memory for the life of the process. Answers whether an assistant model takes
/// images. A model that is not listed, or a catalog that fails to load, gives false: text still goes through.
/// </summary>
public sealed class ChatModelCatalogService(IProviderRouter providers, ILogger<ChatModelCatalogService> logger)
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<ChatModel>> _byProvider = new(StringComparer.Ordinal);

    public async Task<bool> IsImageInputAsync(ModelRef model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var models = await FindModelsAsync(model.ProviderId, cancellationToken);
        if (models is null)
        {
            logger.LogWarning("Chat model catalog of provider {ProviderId} is not available; images are not sent.", model.ProviderId);
            return false;
        }

        return models.Any(m => m.Id == model.ModelId && m.SupportsImageInput);
    }

    /// <summary>Chat models of a provider that can run the assistant (they list tools), sorted by name.</summary>
    public async Task<IReadOnlyList<ChatModel>> ListAssistantModelsAsync(string providerId, CancellationToken cancellationToken)
    {
        var models = await FindModelsAsync(providerId, cancellationToken) ?? [];
        return models
            .Where(m => m.SupportsTools)
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Chat models of a provider, or null when the list cannot be loaded. The caller decides what to do without it;
    /// the failure is logged here.
    /// </summary>
    /// <summary>Context length of the model in tokens; <see cref="TokenEstimator.UnknownContextLength"/> when the catalog does not say (C1.1).</summary>
    public async Task<int> ContextLengthAsync(ModelRef model, CancellationToken cancellationToken)
    {
        var models = await FindModelsAsync(model.ProviderId, cancellationToken);
        return models?.FirstOrDefault(m => m.Id == model.ModelId)?.ContextLength ?? TokenEstimator.UnknownContextLength;
    }

    public async Task<IReadOnlyList<ChatModel>?> FindModelsAsync(string providerId, CancellationToken cancellationToken)
    {
        try
        {
            return await LoadAsync(providerId, cancellationToken);
        }
        catch (ProviderException ex)
        {
            logger.LogWarning(ex, "Chat model catalog of provider {ProviderId} failed to load.", providerId);
            return null;
        }
    }

    private async Task<IReadOnlyList<ChatModel>> LoadAsync(string providerId, CancellationToken cancellationToken)
    {
        if (_byProvider.TryGetValue(providerId, out var cached))
        {
            return cached;
        }

        var loaded = await providers.GetChatClient(providerId).ListChatModelsAsync(cancellationToken);
        _byProvider[providerId] = loaded;
        return loaded;
    }
}
