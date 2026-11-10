using ImageGen.Core.Domain.Models;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public sealed class AudioModelRepositoryTests
{
    [Fact]
    public async Task ReplaceAsync_RoundTripsModelsAndReplacesOnlyProviderRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(cancellationToken);
        var repository = new AudioModelRepository(test.Database);
        var original = Model("old/model", "Old", AudioOutput.ChatAudio);
        var current = Model("new/model", "New", AudioOutput.Speech) with { PricePerInputCharacterUsd = 0.00002m };
        var other = Model("same/model", "Other", AudioOutput.Speech) with { ProviderId = "other" };

        await repository.ReplaceAsync("openrouter", [original], cancellationToken);
        await repository.ReplaceAsync("other", [other], cancellationToken);
        await repository.ReplaceAsync("openrouter", [current], cancellationToken);

        var stored = Assert.Single(await repository.ListAsync("openrouter", cancellationToken));
        Assert.Equal(current, stored);
        Assert.Equal(other, Assert.Single(await repository.ListAsync("other", cancellationToken)));
    }

    private static AudioModel Model(string id, string name, AudioOutput output) => new()
    {
        ProviderId = "openrouter",
        Id = id,
        Name = name,
        Output = output,
        Description = "description",
        FetchedAt = TestData.BaseTime,
    };
}
