using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class SessionRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenGetAsync_ReturnsSameValues()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new SessionRepository(test.Database);
        var session = TestData.Session();
        session.Settings.ImageModelIds = ["a/b", "c/d"];
        session.Settings.RunsPerModel = 3;

        await repository.AddAsync(session, ct);
        var loaded = await repository.GetAsync(session.Id, ct);

        Assert.NotNull(loaded);
        Assert.Equal(session.Title, loaded.Title);
        Assert.Equal(session.CreatedAt, loaded.CreatedAt);
        Assert.Equal(session.UpdatedAt, loaded.UpdatedAt);
        Assert.Equal(["a/b", "c/d"], loaded.Settings.ImageModelIds);
        Assert.Equal(3, loaded.Settings.RunsPerModel);
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new SessionRepository(test.Database);

        Assert.Null(await repository.GetAsync(Guid.NewGuid(), ct));
    }

    [Fact]
    public async Task UpdateAsync_ChangedTitle_IsSaved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new SessionRepository(test.Database);
        var session = TestData.Session();
        await repository.AddAsync(session, ct);

        session.Title = "Renamed";
        await repository.UpdateAsync(session, ct);

        Assert.Equal("Renamed", (await repository.GetAsync(session.Id, ct))!.Title);
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestUpdatedFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new SessionRepository(test.Database);
        var older = TestData.Session(TestData.BaseTime);
        var newer = TestData.Session(TestData.BaseTime.AddMinutes(5));
        await repository.AddAsync(older, ct);
        await repository.AddAsync(newer, ct);

        var list = await repository.ListAsync(ct);

        Assert.Equal([newer.Id, older.Id], list.Select(s => s.Id));
    }

    [Fact]
    public async Task DeleteAsync_SessionWithChildren_DeletesChildren()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessions = new SessionRepository(test.Database);
        var messages = new ChatMessageRepository(test.Database);
        var runs = new RunRepository(test.Database);
        var session = TestData.Session();
        await sessions.AddAsync(session, ct);
        await messages.AddAsync(TestData.Message(session.Id, TestData.BaseTime), ct);
        var run = TestData.Run(session.Id, TestData.BaseTime);
        var generation = TestData.Generation(run, 0);
        await runs.AddRunAsync(run, [generation], ct);

        await sessions.DeleteAsync(session.Id, ct);

        Assert.Null(await sessions.GetAsync(session.Id, ct));
        Assert.Empty(await messages.ListBySessionAsync(session.Id, ct));
        Assert.Null(await runs.GetRunAsync(run.Id, ct));
        Assert.Null(await runs.GetGenerationAsync(generation.Id, ct));
    }
}
