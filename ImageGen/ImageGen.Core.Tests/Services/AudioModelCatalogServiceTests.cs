using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Tests.Services;

public sealed class AudioModelCatalogServiceTests
{
    private readonly IAudioModelProvider _provider = Substitute.For<IAudioModelProvider>();
    private readonly IAudioModelRepository _repository = Substitute.For<IAudioModelRepository>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly CapturingLogger<AudioModelCatalogService> _logger = new();
    private readonly AudioModelCatalogService _service;

    public AudioModelCatalogServiceTests()
    {
        _providers.GetAudioModelProvider(ProviderIds.OpenRouter).Returns(_provider);
        _service = new AudioModelCatalogService(_providers, _repository, _logger);
    }

    [Fact]
    public async Task GetAudioModelsAsync_EmptyCache_LoadsCachesAndSortsByName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _repository.ListAsync(ProviderIds.OpenRouter, cancellationToken).Returns([]);
        IReadOnlyList<AudioModel> loaded = [Model("z/model", "Zulu"), Model("a/model", "Alpha")];
        _provider.ListAudioModelsAsync(cancellationToken).Returns(loaded);

        var models = await _service.GetAudioModelsAsync(forceRefresh: false, cancellationToken);

        Assert.Equal(["Alpha", "Zulu"], models.Select(model => model.Name));
        await _repository.Received(1).ReplaceAsync(ProviderIds.OpenRouter, loaded, cancellationToken);
    }

    [Fact]
    public async Task GetAudioModelsAsync_RefreshFails_ReturnsCache()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        IReadOnlyList<AudioModel> cached = [Model("cached/model", "Cached")];
        _repository.ListAsync(ProviderIds.OpenRouter, cancellationToken).Returns(cached);
        _provider.ListAudioModelsAsync(cancellationToken).Returns<Task<IReadOnlyList<AudioModel>>>(
            _ => throw new ProviderException(ErrorKind.Network, "offline"));

        var models = await _service.GetAudioModelsAsync(forceRefresh: true, cancellationToken);

        Assert.Same(cached, models);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning);
        await _repository.DidNotReceive().ReplaceAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<AudioModel>>(), cancellationToken);
    }

    private static AudioModel Model(string id, string name) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = name,
        Output = AudioOutput.ChatAudio,
        FetchedAt = DateTimeOffset.UnixEpoch,
    };
}
