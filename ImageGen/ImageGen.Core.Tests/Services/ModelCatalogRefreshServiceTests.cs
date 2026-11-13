using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public sealed class ModelCatalogRefreshServiceTests
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly IProviderRouter _router = Substitute.For<IProviderRouter>();
    private readonly ApiKeyService _keys;
    private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
    private readonly IImageModelRepository _images = Substitute.For<IImageModelRepository>();
    private readonly IVideoModelRepository _videos = Substitute.For<IVideoModelRepository>();
    private readonly IAudioModelRepository _audio = Substitute.For<IAudioModelRepository>();
    private readonly IEmbeddingModelRepository _embeddings = Substitute.For<IEmbeddingModelRepository>();
    private readonly ModelCatalogService _imageCatalog;
    private readonly VideoModelCatalogService _videoCatalog;
    private readonly AudioModelCatalogService _audioCatalog;
    private readonly EmbeddingModelCatalogService _embeddingCatalog;
    private readonly CapturingLogger<ModelCatalogRefreshService> _logger = new();

    public ModelCatalogRefreshServiceTests()
    {
        _router.ImageProviderIds.Returns([ProviderIds.OpenRouter, "fal"]);
        _secrets.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("configured-key");
        _keys = new ApiKeyService(_router, _secrets, NullLogger<ApiKeyService>.Instance);

        var imageProvider = Substitute.For<IModelProvider>();
        var falImageProvider = Substitute.For<IModelProvider>();
        var videoProvider = Substitute.For<IVideoModelProvider>();
        var audioProvider = Substitute.For<IAudioModelProvider>();
        var embeddingProvider = Substitute.For<IEmbeddingModelProvider>();
        _router.GetImageProvider(Arg.Any<string>()).Returns(imageProvider);
        _router.GetImageProvider("fal").Returns(falImageProvider);
        _router.GetVideoModelProvider(ProviderIds.OpenRouter).Returns(videoProvider);
        _router.GetAudioModelProvider(ProviderIds.OpenRouter).Returns(audioProvider);
        _router.GetEmbeddingModelProvider(ProviderIds.OpenRouter).Returns(embeddingProvider);
        imageProvider.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns([Image(ProviderIds.OpenRouter, "image")]);
        falImageProvider.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns([Image("fal", "image")]);
        videoProvider.ListVideoModelsAsync(Arg.Any<CancellationToken>()).Returns([Video("video")]);
        audioProvider.ListAudioModelsAsync(Arg.Any<CancellationToken>()).Returns([Audio("audio")]);
        embeddingProvider.ListEmbeddingModelsAsync(Arg.Any<CancellationToken>()).Returns([Embedding("embedding")]);

        _imageCatalog = new ModelCatalogService(_router, _images, NullLogger<ModelCatalogService>.Instance);
        _videoCatalog = new VideoModelCatalogService(_router, _videos, NullLogger<VideoModelCatalogService>.Instance);
        _audioCatalog = new AudioModelCatalogService(_router, _audio, NullLogger<AudioModelCatalogService>.Instance);
        _embeddingCatalog = new EmbeddingModelCatalogService(_router, _embeddings, NullLogger<EmbeddingModelCatalogService>.Instance);
    }

    [Fact]
    public async Task RefreshStaleAsync_FreshCaches_DoesNotCallProviders()
    {
        var ct = TestContext.Current.CancellationToken;
        _images.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var providerId = call.ArgAt<string>(0);
            return Task.FromResult<IReadOnlyList<ImageModel>>([Image(providerId, "fresh", _time.GetUtcNow())]);
        });
        _videos.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Video("fresh", _time.GetUtcNow())]);
        _audio.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Audio("fresh", _time.GetUtcNow())]);
        _embeddings.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Embedding("fresh", _time.GetUtcNow())]);

        var service = CreateService();
        await service.RefreshStaleAsync(ct);

        _ = _router.DidNotReceive().GetImageProvider(Arg.Any<string>());
        _ = _router.DidNotReceive().GetVideoModelProvider(Arg.Any<string>());
        _ = _router.DidNotReceive().GetAudioModelProvider(Arg.Any<string>());
        _ = _router.DidNotReceive().GetEmbeddingModelProvider(Arg.Any<string>());
    }

    [Fact]
    public async Task RefreshStaleAsync_EmptyCaches_LoadsAllCatalogsForConfiguredProviders()
    {
        var ct = TestContext.Current.CancellationToken;
        _images.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _videos.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([]);
        _audio.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([]);
        _embeddings.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([]);

        await CreateService().RefreshStaleAsync(ct);

        await _router.GetImageProvider(ProviderIds.OpenRouter).Received(1).ListImageModelsAsync(Arg.Any<CancellationToken>());
        await _router.GetImageProvider("fal").Received(1).ListImageModelsAsync(Arg.Any<CancellationToken>());
        await _router.GetVideoModelProvider(ProviderIds.OpenRouter).Received(1).ListVideoModelsAsync(Arg.Any<CancellationToken>());
        await _router.GetAudioModelProvider(ProviderIds.OpenRouter).Received(1).ListAudioModelsAsync(Arg.Any<CancellationToken>());
        await _router.GetEmbeddingModelProvider(ProviderIds.OpenRouter).Received(1).ListEmbeddingModelsAsync(Arg.Any<CancellationToken>());
        await _images.Received(1).ReplaceAllAsync(ProviderIds.OpenRouter, Arg.Any<IReadOnlyList<ImageModel>>(), Arg.Any<CancellationToken>());
        await _images.Received(1).ReplaceAllAsync("fal", Arg.Any<IReadOnlyList<ImageModel>>(), Arg.Any<CancellationToken>());
        await _videos.Received(1).ReplaceAsync(ProviderIds.OpenRouter, Arg.Any<IReadOnlyList<VideoModel>>(), Arg.Any<CancellationToken>());
        await _audio.Received(1).ReplaceAsync(ProviderIds.OpenRouter, Arg.Any<IReadOnlyList<AudioModel>>(), Arg.Any<CancellationToken>());
        await _embeddings.Received(1).ReplaceAsync(ProviderIds.OpenRouter, Arg.Any<IReadOnlyList<EmbeddingModel>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshStaleAsync_StaleCacheRefreshesButMissingProviderKeyIsSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        _images.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var providerId = call.ArgAt<string>(0);
            return Task.FromResult<IReadOnlyList<ImageModel>>([Image(providerId, providerId, _time.GetUtcNow().AddDays(-1))]);
        });
        _secrets.GetAsync(SecretNames.ForProvider("fal"), Arg.Any<CancellationToken>()).Returns((string?)null);
        _videos.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Video("stale", _time.GetUtcNow().AddDays(-1))]);
        _audio.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Audio("stale", _time.GetUtcNow().AddDays(-1))]);
        _embeddings.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Embedding("stale", _time.GetUtcNow().AddDays(-1))]);

        await CreateService().RefreshStaleAsync(ct);

        await _router.GetImageProvider(ProviderIds.OpenRouter).Received(1).ListImageModelsAsync(Arg.Any<CancellationToken>());
        _ = _router.DidNotReceive().GetImageProvider("fal");
        Assert.DoesNotContain(_logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RefreshStaleAsync_EmptyCacheProviderError_LogsOnceAndContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        _images.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _videos.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([]);
        _audio.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([]);
        _embeddings.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([]);
        _router.GetImageProvider(ProviderIds.OpenRouter).ListImageModelsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<ImageModel>>>(_ => throw new ProviderException(ErrorKind.Network, "offline"));

        await CreateService().RefreshStaleAsync(ct);

        Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Warning);
        await _router.GetVideoModelProvider(ProviderIds.OpenRouter).Received(1).ListVideoModelsAsync(Arg.Any<CancellationToken>());
    }

    private ModelCatalogRefreshService CreateService() => new(
        _router, _keys, _images, _videos, _audio, _embeddings,
        _imageCatalog, _videoCatalog, _audioCatalog, _embeddingCatalog,
        _time, _logger);

    private static ImageModel Image(string provider, string id, DateTimeOffset? fetched = null) => new()
    {
        ProviderId = provider,
        Id = id,
        Name = id,
        SupportsSeed = false,
        SupportsImageInput = false,
        MaxImagesPerRequest = 1,
        FetchedAt = fetched ?? DateTimeOffset.UnixEpoch,
        Parameters = [],
    };

    private static VideoModel Video(string id, DateTimeOffset? fetched = null) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = id,
        FetchedAt = fetched ?? DateTimeOffset.UnixEpoch,
        Durations = [],
        Resolutions = [],
        AspectRatios = [],
        FrameImages = [],
        PriceSkus = new Dictionary<string, decimal>(),
    };

    private static AudioModel Audio(string id, DateTimeOffset? fetched = null) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = id,
        Output = AudioOutput.ChatAudio,
        FetchedAt = fetched ?? DateTimeOffset.UnixEpoch,
    };

    private static EmbeddingModel Embedding(string id, DateTimeOffset? fetched = null) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = id,
        Description = string.Empty,
        FetchedAt = fetched ?? DateTimeOffset.UnixEpoch,
    };

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
