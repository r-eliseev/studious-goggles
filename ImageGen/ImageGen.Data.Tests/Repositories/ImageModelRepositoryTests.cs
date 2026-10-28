using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class ImageModelRepositoryTests
{
    [Fact]
    public async Task ReplaceAllAsync_TwiceWithDifferentLists_KeepsOnlyLastListSortedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new ImageModelRepository(test.Database);

        await repository.ReplaceAllAsync(ProviderIds.OpenRouter, [Model("old/model", "Old")], ct);
        await repository.ReplaceAllAsync(ProviderIds.OpenRouter, [Model("z/z", "Zeta"), Model("a/a", "Alpha")], ct);

        var list = await repository.ListAsync(ProviderIds.OpenRouter, ct);
        Assert.Equal(["Alpha", "Zeta"], list.Select(m => m.Name));
        Assert.True(list[0].SupportsSeed);
        Assert.Equal(4, list[0].MaxImagesPerRequest);
        Assert.Equal(TestData.BaseTime, list[0].FetchedAt);
    }

    [Fact]
    public async Task ReplaceAllAsync_OneProvider_DoesNotTouchOtherProviders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new ImageModelRepository(test.Database);
        await repository.ReplaceAllAsync("local", [Model("same/id", "Local")], ct);

        await repository.ReplaceAllAsync(ProviderIds.OpenRouter, [Model("same/id", "Open")], ct);
        await repository.ReplaceAllAsync(ProviderIds.OpenRouter, [], ct);

        Assert.Empty(await repository.ListAsync(ProviderIds.OpenRouter, ct));
        var local = await repository.ListAsync("local", ct);
        Assert.Equal(["Local"], local.Select(m => m.Name));
        Assert.Equal("local", local[0].ProviderId);
    }

    [Fact]
    public async Task ListAsync_SameIdInTwoProviders_ReturnsEachProvidersRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new ImageModelRepository(test.Database);
        await repository.ReplaceAllAsync(ProviderIds.OpenRouter, [Model("same/id", "Open")], ct);
        await repository.ReplaceAllAsync("local", [Model("same/id", "Local")], ct);

        Assert.Equal(["Open"], (await repository.ListAsync(ProviderIds.OpenRouter, ct)).Select(m => m.Name));
        Assert.Equal(["Local"], (await repository.ListAsync("local", ct)).Select(m => m.Name));
    }

    private static ImageModel Model(string id, string name) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = name,
        SupportsSeed = true,
        SupportsImageInput = false,
        MaxImagesPerRequest = 4,
        FetchedAt = TestData.BaseTime,
    };
}
