using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;

namespace ImageGen.Core.Tests.Services;

public class ModelCatalogServiceTests
{
    private readonly IModelProvider _client = Substitute.For<IModelProvider>();
    private readonly IImageModelRepository _repository = Substitute.For<IImageModelRepository>();
    private readonly ModelCatalogService _service;

    public ModelCatalogServiceTests()
    {
        _service = new ModelCatalogService(_client, _repository);
    }

    public static ImageModel Model(string id, string name, bool supportsSeed = true) => new()
    {
        Id = id,
        Name = name,
        SupportsSeed = supportsSeed,
        SupportsImageInput = false,
        MaxImagesPerRequest = 1,
        FetchedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task GetImageModelsAsync_CacheNotEmpty_ReturnsCacheWithoutRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([Model("a", "A")]);

        var models = await _service.GetImageModelsAsync(forceRefresh: false, ct);

        Assert.Equal(["a"], models.Select(m => m.Id));
        await _client.DidNotReceive().ListImageModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetImageModelsAsync_CacheEmpty_LoadsSavesAndSortsByName()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        IReadOnlyList<ImageModel> loaded = [Model("z", "Zeta"), Model("a", "Alpha")];
        _client.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns(loaded);

        var models = await _service.GetImageModelsAsync(forceRefresh: false, ct);

        Assert.Equal(["Alpha", "Zeta"], models.Select(m => m.Name));
        await _repository.Received(1).ReplaceAllAsync(loaded, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetImageModelsAsync_ForceRefreshFails_ReturnsCache()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([Model("a", "A")]);
        _client.ListImageModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ImageModel>>(new ProviderException(ErrorKind.Network, "down")));

        var models = await _service.GetImageModelsAsync(forceRefresh: true, ct);

        Assert.Equal(["a"], models.Select(m => m.Id));
    }

    [Fact]
    public async Task GetImageModelsAsync_CacheEmptyAndLoadFails_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        _client.ListImageModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ImageModel>>(new ProviderException(ErrorKind.Network, "down")));

        await Assert.ThrowsAsync<ProviderException>(() => _service.GetImageModelsAsync(forceRefresh: false, ct));
    }

    [Fact]
    public async Task FindAsync_KnownAndUnknownId_ReturnsModelOrNull()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([Model("a", "A")]);

        Assert.NotNull(await _service.FindAsync("a", ct));
        Assert.Null(await _service.FindAsync("missing", ct));
    }
}
