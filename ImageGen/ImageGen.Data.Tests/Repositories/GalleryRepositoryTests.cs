using ImageGen.Core.Domain.Gallery;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Storage;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public sealed class GalleryRepositoryTests
{
    [Fact]
    public async Task ListAsync_EqualTimestampsAcrossPages_HasNoDuplicatesOrGaps()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var runs = new RunRepository(test.Database);
        var at = TestData.BaseTime;
        var generations = new[] { MakeGeneration(session.Id, "a", at), MakeGeneration(session.Id, "b", at), MakeGeneration(session.Id, "c", at.AddMinutes(-1)) };
        for (var i = 0; i < generations.Length; i++)
        {
            var run = TestData.Run(session.Id, at);
            generations[i].RunId = run.Id;
            await runs.AddRunAsync(run, [generations[i]], ct);
        }
        var gallery = new GalleryRepository(test.Database);

        var first = await gallery.ListAsync(new GalleryQuery(2), ct);
        var second = await gallery.ListAsync(new GalleryQuery(2, first.NextCursor), ct);

        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
        Assert.Equal(3, first.Items.Concat(second.Items).Select(item => item.GenerationId).Distinct().Count());
    }

    [Fact]
    public async Task ListAsync_ModelAndDateFiltersApplyBeforePageLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var runs = new RunRepository(test.Database);
        foreach (var generation in new[]
        {
            MakeGeneration(session.Id, "wanted", TestData.BaseTime, "fox"),
            MakeGeneration(session.Id, "other-model", TestData.BaseTime.AddHours(-1), "owl"),
            MakeGeneration(session.Id, "outside", TestData.BaseTime.AddDays(-3), "fox"),
        })
        {
            var run = TestData.Run(session.Id, generation.CompletedAt!.Value);
            generation.RunId = run.Id;
            await runs.AddRunAsync(run, [generation], ct);
        }
        var query = new GalleryQuery(1, Model: new ModelRef("openrouter", "fox"), From: TestData.BaseTime.AddDays(-1), To: TestData.BaseTime.AddDays(1));

        var page = await new GalleryRepository(test.Database).ListAsync(query, ct);

        Assert.Single(page.Items);
        Assert.Equal("wanted", page.Items[0].Prompt);
        Assert.Null(page.NextCursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ListAsync_InvalidPageSize_Throws(int size)
    {
        await using var test = await TestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new GalleryRepository(test.Database)
            .ListAsync(new GalleryQuery(size), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsDistinctModelsThatHaveImages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        var first = MakeGeneration(session.Id, "first", TestData.BaseTime, "fox");
        var duplicate = MakeGeneration(session.Id, "second", TestData.BaseTime, "fox");
        first.RunId = run.Id;
        duplicate.RunId = run.Id;
        await new RunRepository(test.Database).AddRunAsync(run, [first, duplicate], ct);

        var models = await new GalleryRepository(test.Database).ListModelsAsync(ct);

        Assert.Equal([new ModelRef("openrouter", "fox")], models);
    }

    private static Generation MakeGeneration(Guid sessionId, string prompt, DateTimeOffset at, string model = "image") => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        SessionId = sessionId,
        ProviderId = "openrouter",
        ModelId = model,
        Prompt = prompt,
        Seed = 1,
        SeedApplied = true,
        Status = GenerationStatus.Succeeded,
        ImagePath = $"images/{prompt}.png",
        MediaType = "image/png",
        CreatedAt = at,
        CompletedAt = at,
        SortOrder = 0,
    };
}
