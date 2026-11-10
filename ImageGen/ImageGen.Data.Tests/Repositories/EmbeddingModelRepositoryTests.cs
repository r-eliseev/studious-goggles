using ImageGen.Core.Domain.Models;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public sealed class EmbeddingModelRepositoryTests
{
    [Fact]
    public async Task ReplaceAsync_RoundTripsCatalogAndReplacesProviderRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new EmbeddingModelRepository(test.Database);
        var model = new EmbeddingModel
        {
            ProviderId = "openrouter",
            Id = "vendor/model",
            Name = "Vendor Model",
            Description = "Embedding model",
            ContextLength = 8_192,
            PricePerTokenUsd = 0.00000001m,
            FetchedAt = TestData.BaseTime,
        };

        await repository.ReplaceAsync("openrouter", [model], ct);

        var loaded = Assert.Single(await repository.ListAsync("openrouter", ct));
        Assert.Equal(model, loaded);
        await repository.ReplaceAsync("openrouter", [], ct);
        Assert.Empty(await repository.ListAsync("openrouter", ct));
    }
}
