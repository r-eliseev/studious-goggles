using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

public sealed class VideoModelCatalogService(
    IProviderRouter providers,
    IVideoModelRepository repository,
    ILogger<VideoModelCatalogService> logger)
{
    public async Task<IReadOnlyList<VideoModel>> GetVideoModelsAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var cached = await repository.ListAsync(ProviderIds.OpenRouter, cancellationToken);
        if (!forceRefresh && cached.Count > 0)
        {
            logger.LogInformation("Video models of provider {ProviderId}: {Count} from cache.", ProviderIds.OpenRouter, cached.Count);
            return cached;
        }

        IReadOnlyList<VideoModel> loaded;
        try
        {
            loaded = await providers.GetVideoModelProvider(ProviderIds.OpenRouter).ListVideoModelsAsync(cancellationToken);
        }
        catch (ProviderException exception) when (cached.Count > 0)
        {
            logger.LogWarning(
                exception,
                "Could not load video models of provider {ProviderId}; using {Count} cached models.",
                ProviderIds.OpenRouter,
                cached.Count);
            return cached;
        }

        await repository.ReplaceAsync(ProviderIds.OpenRouter, loaded, cancellationToken);
        logger.LogInformation("Video models of provider {ProviderId}: {Count} from provider.", ProviderIds.OpenRouter, loaded.Count);
        return Sort(loaded);
    }

    public async Task<VideoModel?> FindAsync(ModelRef reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var models = await GetVideoModelsAsync(forceRefresh: false, cancellationToken);
        return models.FirstOrDefault(model => model.ProviderId == reference.ProviderId && model.Id == reference.ModelId);
    }

    private static IReadOnlyList<VideoModel> Sort(IEnumerable<VideoModel> models) => models
        .OrderBy(model => model.Name, StringComparer.Ordinal)
        .ThenBy(model => model.ProviderId, StringComparer.Ordinal)
        .ThenBy(model => model.Id, StringComparer.Ordinal)
        .ToList();
}
