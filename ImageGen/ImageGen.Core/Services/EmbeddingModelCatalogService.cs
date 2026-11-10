using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

public sealed class EmbeddingModelCatalogService(
    IProviderRouter providers,
    IEmbeddingModelRepository repository,
    ILogger<EmbeddingModelCatalogService> logger)
{
    public async Task<IReadOnlyList<EmbeddingModel>> GetModelsAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var cached = await repository.ListAsync(ProviderIds.OpenRouter, cancellationToken).ConfigureAwait(false);
        if (!forceRefresh && cached.Count > 0)
        {
            return cached;
        }

        IReadOnlyList<EmbeddingModel> loaded;
        try
        {
            loaded = await providers.GetEmbeddingModelProvider(ProviderIds.OpenRouter)
                .ListEmbeddingModelsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ProviderException exception) when (cached.Count > 0)
        {
            logger.LogWarning(exception, "Could not refresh embedding models; using {Count} cached models.", cached.Count);
            return cached;
        }

        await repository.ReplaceAsync(ProviderIds.OpenRouter, loaded, cancellationToken).ConfigureAwait(false);
        return loaded.OrderBy(model => model.Name, StringComparer.Ordinal)
            .ThenBy(model => model.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<EmbeddingModel?> FindAsync(string modelId, CancellationToken cancellationToken)
    {
        var models = await GetModelsAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        return models.FirstOrDefault(model => model.ProviderId == ProviderIds.OpenRouter && model.Id == modelId);
    }
}
