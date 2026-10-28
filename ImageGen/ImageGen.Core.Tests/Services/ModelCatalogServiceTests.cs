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
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly ModelCatalogService _service;

    public ModelCatalogServiceTests()
    {
        _providers.GetImageProvider(ProviderIds.OpenRouter).Returns(_client);
        _providers.ImageProviderIds.Returns([ProviderIds.OpenRouter]);
        _service = new ModelCatalogService(_providers, _repository);
    }

    public static ImageModel Model(string id, string name, bool supportsSeed = true, string providerId = ProviderIds.OpenRouter) => new()
    {
        ProviderId = providerId,
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
        _repository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Model("a", "A")]);

        var models = await _service.GetImageModelsAsync(ProviderIds.OpenRouter, forceRefresh: false, ct);

        Assert.Equal(["a"], models.Select(m => m.Id));
        await _client.DidNotReceive().ListImageModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetImageModelsAsync_CacheEmpty_LoadsSavesForThatProviderAndSortsByName()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([]);
        IReadOnlyList<ImageModel> loaded = [Model("z", "Zeta"), Model("a", "Alpha")];
        _client.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns(loaded);

        var models = await _service.GetImageModelsAsync(ProviderIds.OpenRouter, forceRefresh: false, ct);

        Assert.Equal(["Alpha", "Zeta"], models.Select(m => m.Name));
        await _repository.Received(1).ReplaceAllAsync(ProviderIds.OpenRouter, loaded, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetImageModelsAsync_ForceRefreshFails_ReturnsCache()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Model("a", "A")]);
        _client.ListImageModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ImageModel>>(new ProviderException(ErrorKind.Network, "down")));

        var models = await _service.GetImageModelsAsync(ProviderIds.OpenRouter, forceRefresh: true, ct);

        Assert.Equal(["a"], models.Select(m => m.Id));
        await _repository.DidNotReceive().ReplaceAllAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ImageModel>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetImageModelsAsync_CacheEmptyAndLoadFails_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([]);
        _client.ListImageModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<ImageModel>>(new ProviderException(ErrorKind.Network, "down")));

        await Assert.ThrowsAsync<ProviderException>(() => _service.GetImageModelsAsync(ProviderIds.OpenRouter, forceRefresh: false, ct));
    }

    [Fact]
    public async Task GetImageModelsAsync_UnknownProvider_ThrowsWithoutRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync("fal", Arg.Any<CancellationToken>()).Returns([]);
        _providers.GetImageProvider("fal").Returns<IModelProvider>(_ => throw new ProviderException(ErrorKind.ModelUnavailable, "Provider 'fal' is not configured."));

        var ex = await Assert.ThrowsAsync<ProviderException>(() => _service.GetImageModelsAsync("fal", forceRefresh: false, ct));

        Assert.Equal(ErrorKind.ModelUnavailable, ex.Kind);
    }

    [Fact]
    public async Task GetAllImageModelsAsync_TwoProviders_ReturnsBothSortedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        var other = Substitute.For<IModelProvider>();
        _providers.ImageProviderIds.Returns([ProviderIds.OpenRouter, "local"]);
        _providers.GetImageProvider("local").Returns(other);
        _repository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Model("a", "Beta")]);
        _repository.ListAsync("local", Arg.Any<CancellationToken>()).Returns([Model("a", "Alpha", providerId: "local")]);

        var models = await _service.GetAllImageModelsAsync(forceRefresh: false, ct);

        Assert.Equal([("local", "a"), (ProviderIds.OpenRouter, "a")], models.Select(m => (m.ProviderId, m.Id)));
    }

    [Fact]
    public async Task FindAsync_SameModelIdUnderTwoProviders_ReturnsTheReferencedProvidersModel()
    {
        var ct = TestContext.Current.CancellationToken;
        _providers.ImageProviderIds.Returns([ProviderIds.OpenRouter, "local"]);
        _repository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Model("a", "Open")]);
        _repository.ListAsync("local", Arg.Any<CancellationToken>()).Returns([Model("a", "Local", providerId: "local")]);

        var found = await _service.FindAsync(new ModelRef("local", "a"), ct);

        Assert.Equal("Local", found!.Name);
        Assert.Equal("local", found.ProviderId);
    }

    [Fact]
    public async Task FindAsync_KnownAndUnknownId_ReturnsModelOrNull()
    {
        var ct = TestContext.Current.CancellationToken;
        _repository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([Model("a", "A")]);

        Assert.NotNull(await _service.FindAsync(new ModelRef(ProviderIds.OpenRouter, "a"), ct));
        Assert.Null(await _service.FindAsync(new ModelRef(ProviderIds.OpenRouter, "missing"), ct));
    }
}
