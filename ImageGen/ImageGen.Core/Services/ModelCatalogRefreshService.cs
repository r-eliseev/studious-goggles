using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>Refreshes model catalogs whose cached data is empty or at least one day old.</summary>
public sealed class ModelCatalogRefreshService(
    IProviderRouter providers,
    ApiKeyService apiKeys,
    IImageModelRepository imageRepository,
    IVideoModelRepository videoRepository,
    IAudioModelRepository audioRepository,
    IEmbeddingModelRepository embeddingRepository,
    ModelCatalogService images,
    VideoModelCatalogService videos,
    AudioModelCatalogService audio,
    EmbeddingModelCatalogService embeddings,
    TimeProvider timeProvider,
    ILogger<ModelCatalogRefreshService> logger)
{
    private static readonly TimeSpan MaxCatalogAge = TimeSpan.FromHours(24);

    public async Task RefreshStaleAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var providerId in providers.ImageProviderIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await apiKeys.HasKeyAsync(providerId, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            try
            {
                var cached = await imageRepository.ListAsync(providerId, cancellationToken).ConfigureAwait(false);
                if (IsStale(cached, model => model.FetchedAt, now))
                {
                    _ = await images.GetImageModelsAsync(providerId, forceRefresh: true, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ProviderException exception)
            {
                LogFailure("image", providerId, exception);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogFailure("image", providerId, exception);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await apiKeys.HasKeyAsync(ProviderIds.OpenRouter, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await RefreshAsync("video", ProviderIds.OpenRouter,
            async ct => IsStale(await videoRepository.ListAsync(ProviderIds.OpenRouter, ct).ConfigureAwait(false), model => model.FetchedAt, now),
            ct => videos.GetVideoModelsAsync(forceRefresh: true, ct), cancellationToken).ConfigureAwait(false);
        await RefreshAsync("audio", ProviderIds.OpenRouter,
            async ct => IsStale(await audioRepository.ListAsync(ProviderIds.OpenRouter, ct).ConfigureAwait(false), model => model.FetchedAt, now),
            ct => audio.GetAudioModelsAsync(forceRefresh: true, ct), cancellationToken).ConfigureAwait(false);
        await RefreshAsync("embedding", ProviderIds.OpenRouter,
            async ct => IsStale(await embeddingRepository.ListAsync(ProviderIds.OpenRouter, ct).ConfigureAwait(false), model => model.FetchedAt, now),
            ct => embeddings.GetModelsAsync(forceRefresh: true, ct), cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshAsync<T>(string catalog, string providerId, Func<CancellationToken, Task<bool>> stale, Func<CancellationToken, Task<IReadOnlyList<T>>> refresh, CancellationToken cancellationToken)
    {
        try
        {
            if (await stale(cancellationToken).ConfigureAwait(false))
            {
                _ = await refresh(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (ProviderException exception)
        {
            LogFailure(catalog, providerId, exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure(catalog, providerId, exception);
        }
    }

    private void LogFailure(string catalog, string providerId, Exception exception) =>
        logger.LogWarning(exception, "Could not refresh {Catalog} model catalog for provider {ProviderId}.", catalog, providerId);

    private bool IsStale<T>(IReadOnlyList<T> models, Func<T, DateTimeOffset> fetchedAt, DateTimeOffset now) =>
        models.Count == 0 || models.Min(fetchedAt) <= now - MaxCatalogAge;
}
