using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

public sealed class AudioModelCatalogService(
    IProviderRouter providers,
    IAudioModelRepository repository,
    ILogger<AudioModelCatalogService> logger)
{
    public async Task<IReadOnlyList<AudioModel>> GetAudioModelsAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var cached = await repository.ListAsync(ProviderIds.OpenRouter, cancellationToken);
        if (!forceRefresh && cached.Count > 0)
        {
            logger.LogInformation("Audio models of provider {ProviderId}: {Count} from cache.", ProviderIds.OpenRouter, cached.Count);
            return cached;
        }

        IReadOnlyList<AudioModel> loaded;
        try
        {
            loaded = await providers.GetAudioModelProvider(ProviderIds.OpenRouter).ListAudioModelsAsync(cancellationToken);
        }
        catch (ProviderException exception) when (cached.Count > 0)
        {
            logger.LogWarning(
                exception,
                "Could not load audio models of provider {ProviderId}; using {Count} cached models.",
                ProviderIds.OpenRouter,
                cached.Count);
            return cached;
        }

        await repository.ReplaceAsync(ProviderIds.OpenRouter, loaded, cancellationToken);
        logger.LogInformation("Audio models of provider {ProviderId}: {Count} from provider.", ProviderIds.OpenRouter, loaded.Count);
        return Sort(loaded);
    }

    public async Task<AudioModel?> FindAsync(ModelRef reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var models = await GetAudioModelsAsync(forceRefresh: false, cancellationToken);
        return models.FirstOrDefault(model => model.ProviderId == reference.ProviderId && model.Id == reference.ModelId);
    }

    private static IReadOnlyList<AudioModel> Sort(IEnumerable<AudioModel> models) => models
        .OrderBy(model => model.Name, StringComparer.Ordinal)
        .ThenBy(model => model.ProviderId, StringComparer.Ordinal)
        .ThenBy(model => model.Id, StringComparer.Ordinal)
        .ToList();
}
