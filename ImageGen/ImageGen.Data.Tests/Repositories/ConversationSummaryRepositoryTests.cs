using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class ConversationSummaryRepositoryTests
{
    private static ConversationSummary Summary(Guid sessionId, string text, decimal? cost = 0.002m) => new()
    {
        SessionId = sessionId,
        Text = text,
        CoversUpToMessageId = Guid.NewGuid(),
        Model = new ModelRef(ProviderIds.OpenRouter, "vendor/assistant"),
        CostUsd = cost,
        CreatedAt = TestData.BaseTime,
    };

    [Fact]
    public async Task Upsert_ThenGet_ReturnsSame()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new ConversationSummaryRepository(test.Database);
        var summary = Summary(session.Id, "The user wants a red fox.");

        await repository.UpsertAsync(summary, ct);
        var loaded = await repository.GetAsync(session.Id, ct);

        Assert.NotNull(loaded);
        Assert.Equal(summary.Text, loaded.Text);
        Assert.Equal(summary.CoversUpToMessageId, loaded.CoversUpToMessageId);
        Assert.Equal(summary.Model, loaded.Model);
        Assert.Equal(0.002m, loaded.CostUsd);
        Assert.Equal(summary.CreatedAt, loaded.CreatedAt);
    }

    [Fact]
    public async Task Get_NoSummary_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);

        Assert.Null(await new ConversationSummaryRepository(test.Database).GetAsync(session.Id, ct));
    }

    [Fact]
    public async Task Upsert_Twice_KeepsLast()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new ConversationSummaryRepository(test.Database);

        await repository.UpsertAsync(Summary(session.Id, "first", cost: null), ct);
        await repository.UpsertAsync(Summary(session.Id, "second", cost: 0.01m), ct);

        var loaded = await repository.GetAsync(session.Id, ct);
        Assert.Equal("second", loaded!.Text);
        Assert.Equal(0.01m, loaded.CostUsd);
    }

    [Fact]
    public async Task DeleteSession_DeletesSummary()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        var sessions = new SessionRepository(test.Database);
        await sessions.AddAsync(session, ct);
        var repository = new ConversationSummaryRepository(test.Database);
        await repository.UpsertAsync(Summary(session.Id, "summary"), ct);

        await sessions.DeleteAsync(session.Id, ct);

        Assert.Null(await repository.GetAsync(session.Id, ct));
    }
}
