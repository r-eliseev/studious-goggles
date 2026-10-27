using ImageGen.Core.Domain;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;

namespace ImageGen.Core.Services;

public sealed class ModelCatalogService(IOpenRouterClient client, IImageModelRepository repository)
{
    /// <summary>
    /// Returns image models sorted by name. Uses the cache unless it is empty or <paramref name="forceRefresh"/> is true.
    /// If loading fails and the cache is not empty, returns the cache.
    /// </summary>
    public async Task<IReadOnlyList<ImageModel>> GetImageModelsAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var cached = await repository.ListAsync(cancellationToken);
        if (!forceRefresh && cached.Count > 0)
        {
            return cached;
        }

        IReadOnlyList<ImageModel> loaded;
        try
        {
            loaded = await client.ListImageModelsAsync(cancellationToken);
        }
        catch (ProviderException) when (cached.Count > 0)
        {
            return cached;
        }

        await repository.ReplaceAllAsync(loaded, cancellationToken);
        return loaded.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Finds a model by id using the cache (loads it when empty). Returns null when unknown.</summary>
    public async Task<ImageModel?> FindAsync(string modelId, CancellationToken cancellationToken)
    {
        var models = await GetImageModelsAsync(forceRefresh: false, cancellationToken);
        return models.FirstOrDefault(m => m.Id == modelId);
    }
}
