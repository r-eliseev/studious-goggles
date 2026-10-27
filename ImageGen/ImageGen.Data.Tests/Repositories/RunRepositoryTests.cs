using ImageGen.Core.Domain;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class RunRepositoryTests
{
    [Fact]
    public async Task AddRunAsync_ThenGet_ReturnsRunAndGenerations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new RunRepository(test.Database);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        var g0 = TestData.Generation(run, 0);
        var g1 = TestData.Generation(run, 1);

        await repository.AddRunAsync(run, [g0, g1], ct);

        var loadedRun = await repository.GetRunAsync(run.Id, ct);
        Assert.NotNull(loadedRun);
        Assert.Equal(run.Prompt, loadedRun.Prompt);
        Assert.Equal(RunOrigin.Assistant, loadedRun.Origin);
        var loaded = await repository.GetGenerationAsync(g1.Id, ct);
        Assert.NotNull(loaded);
        Assert.Equal(g1.Seed, loaded.Seed);
        Assert.True(loaded.SeedApplied);
        Assert.Equal(GenerationStatus.Queued, loaded.Status);
        Assert.Null(loaded.ImagePath);
        Assert.Null(loaded.CostUsd);
        Assert.Null(loaded.ErrorKind);
        Assert.Equal(1, loaded.SortOrder);
    }

    [Fact]
    public async Task UpdateGenerationAsync_AllFields_AreSaved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new RunRepository(test.Database);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        var generation = TestData.Generation(run, 0);
        await repository.AddRunAsync(run, [generation], ct);

        generation.Status = GenerationStatus.Failed;
        generation.ImagePath = "images/x/y.png";
        generation.MediaType = "image/png";
        generation.CostUsd = 0.0123m;
        generation.ErrorKind = ErrorKind.RateLimited;
        generation.ErrorMessage = "slow down";
        generation.CompletedAt = TestData.BaseTime.AddSeconds(7);
        await repository.UpdateGenerationAsync(generation, ct);

        var loaded = await repository.GetGenerationAsync(generation.Id, ct);
        Assert.NotNull(loaded);
        Assert.Equal(GenerationStatus.Failed, loaded.Status);
        Assert.Equal("images/x/y.png", loaded.ImagePath);
        Assert.Equal("image/png", loaded.MediaType);
        Assert.Equal(0.0123m, loaded.CostUsd);
        Assert.Equal(ErrorKind.RateLimited, loaded.ErrorKind);
        Assert.Equal("slow down", loaded.ErrorMessage);
        Assert.Equal(TestData.BaseTime.AddSeconds(7), loaded.CompletedAt);
    }

    [Fact]
    public async Task ListGenerationsBySessionAsync_OrdersByRunTimeThenSortOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new RunRepository(test.Database);
        var later = TestData.Run(session.Id, TestData.BaseTime.AddMinutes(1));
        var earlier = TestData.Run(session.Id, TestData.BaseTime);
        var l0 = TestData.Generation(later, 0);
        var e1 = TestData.Generation(earlier, 1);
        var e0 = TestData.Generation(earlier, 0);
        await repository.AddRunAsync(later, [l0], ct);
        await repository.AddRunAsync(earlier, [e1, e0], ct);

        var list = await repository.ListGenerationsBySessionAsync(session.Id, ct);
        var runs = await repository.ListRunsBySessionAsync(session.Id, ct);

        Assert.Equal([e0.Id, e1.Id, l0.Id], list.Select(g => g.Id));
        Assert.Equal([earlier.Id, later.Id], runs.Select(r => r.Id));
    }

    [Fact]
    public async Task MarkUnfinishedAsInterruptedAsync_OnlyQueuedAndRunning_AreMarked()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new RunRepository(test.Database);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        var queued = TestData.Generation(run, 0, GenerationStatus.Queued);
        var running = TestData.Generation(run, 1, GenerationStatus.Running);
        var succeeded = TestData.Generation(run, 2, GenerationStatus.Succeeded);
        await repository.AddRunAsync(run, [queued, running, succeeded], ct);
        var now = TestData.BaseTime.AddHours(1);

        var count = await repository.MarkUnfinishedAsInterruptedAsync(now, ct);

        Assert.Equal(2, count);
        foreach (var id in new[] { queued.Id, running.Id })
        {
            var loaded = (await repository.GetGenerationAsync(id, ct))!;
            Assert.Equal(GenerationStatus.Failed, loaded.Status);
            Assert.Equal(ErrorKind.Interrupted, loaded.ErrorKind);
            Assert.Equal(now, loaded.CompletedAt);
        }

        Assert.Equal(GenerationStatus.Succeeded, (await repository.GetGenerationAsync(succeeded.Id, ct))!.Status);
    }
}
