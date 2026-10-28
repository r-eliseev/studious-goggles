using System.Collections.Concurrent;
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
        IReadOnlyList<ChatModel> models;
        try
        {
            models = await LoadAsync(model.ProviderId, cancellationToken);
        }
        catch (ProviderException ex)
        {
            logger.LogWarning(ex, "Chat model catalog of provider {ProviderId} is not available; images are not sent.", model.ProviderId);
            return false;
        }

        return models.Any(m => m.Id == model.ModelId && m.SupportsImageInput);
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
