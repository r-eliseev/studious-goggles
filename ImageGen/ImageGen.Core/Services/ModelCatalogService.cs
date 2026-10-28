using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;

namespace ImageGen.Core.Services;

public sealed class ModelCatalogService(IProviderRouter providers, IImageModelRepository repository)
{
    /// <summary>
    /// Returns the image models of one provider sorted by name. Uses the cache unless it is empty or
    /// <paramref name="forceRefresh"/> is true. If loading fails and the cache is not empty, returns the cache.
    /// </summary>
    public async Task<IReadOnlyList<ImageModel>> GetImageModelsAsync(
        string providerId,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var cached = await repository.ListAsync(providerId, cancellationToken);
        if (!forceRefresh && cached.Count > 0)
        {
            return cached;
        }

        IReadOnlyList<ImageModel> loaded;
        try
        {
            loaded = await providers.GetImageProvider(providerId).ListImageModelsAsync(cancellationToken);
        }
        catch (ProviderException) when (cached.Count > 0)
        {
            return cached;
        }

        await repository.ReplaceAllAsync(providerId, loaded, cancellationToken);
        return loaded.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Image models of every provider that generates images, sorted by name.</summary>
    public async Task<IReadOnlyList<ImageModel>> GetAllImageModelsAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var all = new List<ImageModel>();
        foreach (var providerId in providers.ImageProviderIds)
        {
            all.AddRange(await GetImageModelsAsync(providerId, forceRefresh, cancellationToken));
        }

        return all.OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.ProviderId, StringComparer.Ordinal).ToList();
    }

    /// <summary>Finds a model by reference using the cache (loads it when empty). Returns null when unknown.</summary>
    public async Task<ImageModel?> FindAsync(ModelRef reference, CancellationToken cancellationToken)
    {
        var models = await GetImageModelsAsync(reference.ProviderId, forceRefresh: false, cancellationToken);
        return models.FirstOrDefault(m => m.Id == reference.ModelId);
    }
}
