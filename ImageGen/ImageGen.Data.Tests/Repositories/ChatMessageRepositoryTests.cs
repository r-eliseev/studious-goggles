using ImageGen.Core.Domain;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class ChatMessageRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenList_ReturnsMessagesInCreationOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new ChatMessageRepository(test.Database);
        var second = TestData.Message(session.Id, TestData.BaseTime.AddSeconds(2), "second");
        var first = TestData.Message(session.Id, TestData.BaseTime.AddSeconds(1), "first");
        var runId = Guid.NewGuid();
        second.Role = ChatRole.Assistant;
        second.RunId = runId;
        second.IsError = true;

        await repository.AddAsync(second, ct);
        await repository.AddAsync(first, ct);
        var list = await repository.ListBySessionAsync(session.Id, ct);

        Assert.Equal(["first", "second"], list.Select(m => m.Text));
        Assert.Equal(ChatRole.Assistant, list[1].Role);
        Assert.Equal(runId, list[1].RunId);
        Assert.True(list[1].IsError);
        Assert.Null(list[0].RunId);
        Assert.False(list[0].IsError);
    }

    [Fact]
    public async Task ListBySessionAsync_OtherSession_IsNotReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessions = new SessionRepository(test.Database);
        var a = TestData.Session();
        var b = TestData.Session();
        await sessions.AddAsync(a, ct);
        await sessions.AddAsync(b, ct);
        var repository = new ChatMessageRepository(test.Database);
        await repository.AddAsync(TestData.Message(a.Id, TestData.BaseTime), ct);

        Assert.Empty(await repository.ListBySessionAsync(b.Id, ct));
    }
}
