using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Tests.Services;

public sealed class VideoModelCatalogServiceTests
{
    private readonly IVideoModelProvider _provider = Substitute.For<IVideoModelProvider>();
    private readonly IVideoModelRepository _repository = Substitute.For<IVideoModelRepository>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly CapturingLogger<VideoModelCatalogService> _logger = new();
    private readonly VideoModelCatalogService _service;

    public VideoModelCatalogServiceTests()
    {
        _providers.GetVideoModelProvider(ProviderIds.OpenRouter).Returns(_provider);
        _service = new VideoModelCatalogService(_providers, _repository, _logger);
    }

    [Fact]
    public async Task GetVideoModelsAsync_EmptyCache_LoadsCachesAndSortsByName()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(ProviderIds.OpenRouter, ct).Returns([]);
        IReadOnlyList<VideoModel> loaded = [Model("z/video", "Zeta"), Model("a/video", "Alpha")];
        _provider.ListVideoModelsAsync(ct).Returns(loaded);

        var models = await _service.GetVideoModelsAsync(forceRefresh: false, ct);

        Assert.Equal(["Alpha", "Zeta"], models.Select(model => model.Name));
        await _repository.Received(1).ReplaceAsync(ProviderIds.OpenRouter, loaded, ct);
    }

    [Fact]
    public async Task GetVideoModelsAsync_RefreshFails_ReturnsCacheAndLogsWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        IReadOnlyList<VideoModel> cached = [Model("cached/video", "Cached")];
        _repository.ListAsync(ProviderIds.OpenRouter, ct).Returns(cached);
        _provider.ListVideoModelsAsync(ct).Returns<Task<IReadOnlyList<VideoModel>>>(
            _ => throw new ProviderException(ErrorKind.Network, "offline"));

        var models = await _service.GetVideoModelsAsync(forceRefresh: true, ct);

        Assert.Same(cached, models);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning);
        await _repository.DidNotReceive().ReplaceAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<VideoModel>>(), ct);
    }

    [Fact]
    public async Task FindAsync_UsesCachedCatalogAndMatchesProviderModelId()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(ProviderIds.OpenRouter, ct).Returns([Model("vendor/video", "Video")]);

        var found = await _service.FindAsync(new ModelRef(ProviderIds.OpenRouter, "vendor/video"), ct);

        Assert.Equal("vendor/video", found!.Id);
        await _provider.DidNotReceive().ListVideoModelsAsync(Arg.Any<CancellationToken>());
    }

    private static VideoModel Model(string id, string name) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = name,
        Durations = [4],
        Resolutions = [],
        AspectRatios = [],
        FrameImages = [],
        GeneratesAudio = null,
        SupportsSeed = null,
        PriceSkus = new Dictionary<string, decimal>(),
        FetchedAt = DateTimeOffset.UnixEpoch,
    };
}
