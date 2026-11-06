using ImageGen.Core.Domain.Models;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public sealed class VideoModelRepositoryTests
{
    [Fact]
    public async Task ReplaceAsync_Twice_KeepsOnlyLatestModelsAndRoundTripsFields()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new VideoModelRepository(test.Database);
        var old = Model("old/video", "Old");
        var current = Model("new/video", "New");
        current.PriceSkus = new Dictionary<string, decimal> { ["duration_seconds_with_audio"] = 0.12m };
        current.FrameImages = ["first_frame", "last_frame"];

        await repository.ReplaceAsync("openrouter", [old], ct);
        await repository.ReplaceAsync("openrouter", [current], ct);

        var model = Assert.Single(await repository.ListAsync("openrouter", ct));
        Assert.Equal("new/video", model.Id);
        Assert.Equal(current.PriceSkus, model.PriceSkus);
        Assert.Equal(current.Durations, model.Durations);
        Assert.Equal(current.FrameImages, model.FrameImages);
        Assert.Equal(current.FetchedAt, model.FetchedAt);
    }

    [Fact]
    public async Task ReplaceAsync_OneProvider_DoesNotDeleteOtherProviders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new VideoModelRepository(test.Database);
        await repository.ReplaceAsync("local", [Model("same/video", "Local")], ct);
        await repository.ReplaceAsync("openrouter", [Model("same/video", "OpenRouter")], ct);
        await repository.ReplaceAsync("openrouter", [], ct);

        Assert.Empty(await repository.ListAsync("openrouter", ct));
        Assert.Equal("Local", Assert.Single(await repository.ListAsync("local", ct)).Name);
    }

    private static VideoModel Model(string id, string name) => new()
    {
        ProviderId = "openrouter",
        Id = id,
        Name = name,
        Durations = [4, 8],
        Resolutions = ["720p"],
        AspectRatios = ["16:9"],
        FrameImages = [],
        GeneratesAudio = true,
        SupportsSeed = true,
        PriceSkus = new Dictionary<string, decimal>(),
        FetchedAt = TestData.BaseTime,
    };
}
