using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Search;
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
        session.Settings.ImageModels = [new ModelRef("openrouter", "a/b"), new ModelRef("local", "c/d")];
        session.Settings.RunsPerModel = 3;

        await repository.AddAsync(session, ct);
        var loaded = await repository.GetAsync(session.Id, ct);

        Assert.NotNull(loaded);
        Assert.Equal(session.Title, loaded.Title);
        Assert.Equal(session.CreatedAt, loaded.CreatedAt);
        Assert.Equal(session.UpdatedAt, loaded.UpdatedAt);
        Assert.Equal(session.Settings.ImageModels, loaded.Settings.ImageModels);
        Assert.Equal(3, loaded.Settings.RunsPerModel);
    }

    [Fact]
    public async Task GetAsync_OldSettingsJson_UsesVideoDefaults()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var repository = new SessionRepository(test.Database);
        var session = TestData.Session();
        await repository.AddAsync(session, ct);
        await using (var connection = await test.Database.OpenAsync(ct))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE sessions SET settings_json = @settings WHERE id = @id;";
            command.Parameters.AddWithValue("@settings", "{\"imageModelIds\":[],\"runsPerModel\":1}");
            command.Parameters.AddWithValue("@id", session.Id.ToString("N"));
            await command.ExecuteNonQueryAsync(ct);
        }

        var loaded = await repository.GetAsync(session.Id, ct);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Settings.VideoModels);
        Assert.Equal(VideoParameters.Default, loaded.Settings.VideoParameters);
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

    [Fact]
    public async Task DeleteAllAsync_DeletesHistoryAndKeepsExpenses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessions = new SessionRepository(test.Database);
        var messages = new ChatMessageRepository(test.Database);
        var runs = new RunRepository(test.Database);
        var first = TestData.Session();
        var second = TestData.Session();
        await sessions.AddAsync(first, ct);
        await sessions.AddAsync(second, ct);
        await messages.AddAsync(TestData.Message(first.Id, TestData.BaseTime), ct);
        var run = TestData.Run(first.Id, TestData.BaseTime);
        await runs.AddRunAsync(run, [TestData.Generation(run, 0)], ct);
        var embeddings = new TextEmbeddingRepository(test.Database);
        await embeddings.UpsertBatchAsync([
            new TextEmbedding($"title:{first.Id:D}", first.Id, SearchTextKind.Title, "hash", "model", 1, [1], TestData.BaseTime)
        ], null, ct);
        await using (var connection = await test.Database.OpenAsync(ct))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO expenses (operation_id, kind, provider_id, model_id, cost_usd, occurred_at) VALUES ('keep', 0, 'openrouter', 'model', '0.25', @occurred);";
            command.Parameters.AddWithValue("@occurred", SqliteValues.FromDate(TestData.BaseTime));
            await command.ExecuteNonQueryAsync(ct);
        }

        var deleted = await sessions.DeleteAllAsync(ct);

        Assert.Equal(2, deleted);
        Assert.Empty(await sessions.ListAsync(ct));
        Assert.Empty(await messages.ListBySessionAsync(first.Id, ct));
        Assert.Empty(await runs.ListRunsBySessionAsync(first.Id, ct));
        Assert.Empty(await embeddings.ListAllAsync(ct));
        Assert.Equal(1, await new ExpenseRepository(test.Database).CountAsync(ct));
    }
}
