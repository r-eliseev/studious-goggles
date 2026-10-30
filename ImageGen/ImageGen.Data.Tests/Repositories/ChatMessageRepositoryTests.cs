using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests.Repositories;

public class ChatMessageRepositoryTests
{
    [Fact]
    public async Task AddUserWithAttachmentsAsync_MissingSecondAttachment_RollsBackMessageAndFirstLink()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var attachments = new ChatAttachmentRepository(test.Database);
        var first = new ChatAttachment
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            RelativePath = "images/a.png",
            MediaType = "image/png",
            CreatedAt = TestData.BaseTime,
        };
        await attachments.AddAsync(first, ct);
        var message = TestData.Message(session.Id, TestData.BaseTime.AddSeconds(1), "question");
        message.Role = ChatRole.User;
        var repository = new ChatMessageRepository(test.Database);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.AddUserWithAttachmentsAsync(message, [first.Id, Guid.NewGuid()], ct));

        Assert.Empty(await repository.ListBySessionAsync(session.Id, ct));
        Assert.Null((await attachments.GetAsync(first.Id, ct))!.MessageId);
    }

    [Fact]
    public async Task AddUserWithAttachmentsAsync_NoAttachments_InsertsUserMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var message = TestData.Message(session.Id, TestData.BaseTime, "question");

        await new ChatMessageRepository(test.Database).AddUserWithAttachmentsAsync(message, [], ct);

        Assert.Equal(message.Id, Assert.Single(await new ChatMessageRepository(test.Database).ListBySessionAsync(session.Id, ct)).Id);
    }

    [Fact]
    public async Task AddUserWithAttachmentsAsync_TwoAttachments_InsertsAndLinksBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var attachments = new ChatAttachmentRepository(test.Database);
        var first = Attachment(session.Id);
        var second = Attachment(session.Id);
        await attachments.AddAsync(first, ct);
        await attachments.AddAsync(second, ct);
        var message = TestData.Message(session.Id, TestData.BaseTime, "question");

        await new ChatMessageRepository(test.Database).AddUserWithAttachmentsAsync(message, [first.Id, second.Id], ct);

        Assert.Equal(message.Id, Assert.Single(await new ChatMessageRepository(test.Database).ListBySessionAsync(session.Id, ct)).Id);
        Assert.Equal([first.Id, second.Id], (await attachments.ListByMessageAsync(message.Id, ct)).Select(item => item.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddUserWithAttachmentsAsync_InvalidAttachment_RollsBackMessageAndEarlierLinks(bool alreadyLinked)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessions = new SessionRepository(test.Database);
        var session = TestData.Session();
        var otherSession = TestData.Session();
        await sessions.AddAsync(session, ct);
        await sessions.AddAsync(otherSession, ct);
        var attachments = new ChatAttachmentRepository(test.Database);
        var first = Attachment(session.Id);
        var second = Attachment(alreadyLinked ? session.Id : otherSession.Id);
        await attachments.AddAsync(first, ct);
        await attachments.AddAsync(second, ct);
        var existingMessage = TestData.Message(session.Id, TestData.BaseTime, "existing");
        var message = TestData.Message(session.Id, TestData.BaseTime.AddSeconds(1), "question");
        if (alreadyLinked)
        {
            await new ChatMessageRepository(test.Database).AddUserWithAttachmentsAsync(existingMessage, [second.Id], ct);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ChatMessageRepository(test.Database).AddUserWithAttachmentsAsync(message, [first.Id, second.Id], ct));

        Assert.Equal(alreadyLinked ? 1 : 0, (await new ChatMessageRepository(test.Database).ListBySessionAsync(session.Id, ct)).Count);
        Assert.Null((await attachments.GetAsync(first.Id, ct))!.MessageId);
        Assert.Equal(alreadyLinked ? existingMessage.Id : null, (await attachments.GetAsync(second.Id, ct))!.MessageId);
    }

    [Fact]
    public async Task AddUserWithAttachmentsAsync_DuplicateIdsOrAssistantRole_RejectsWithoutWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var attachment = Attachment(session.Id);
        var attachments = new ChatAttachmentRepository(test.Database);
        await attachments.AddAsync(attachment, ct);
        var repository = new ChatMessageRepository(test.Database);
        var duplicate = TestData.Message(session.Id, TestData.BaseTime);
        var assistant = TestData.Message(session.Id, TestData.BaseTime);
        assistant.Role = ChatRole.Assistant;

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddUserWithAttachmentsAsync(duplicate, [attachment.Id, attachment.Id], ct));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddUserWithAttachmentsAsync(assistant, [], ct));

        Assert.Empty(await repository.ListBySessionAsync(session.Id, ct));
        Assert.Null((await attachments.GetAsync(attachment.Id, ct))!.MessageId);
    }

    [Fact]
    public async Task AddUserWithAttachmentsAsync_CanceledToken_DoesNotInsertMessage()
    {
        await using var test = await TestDatabase.CreateAsync(CancellationToken.None);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ChatMessageRepository(test.Database).AddUserWithAttachmentsAsync(TestData.Message(session.Id, TestData.BaseTime), [], cancellation.Token));

        Assert.Empty(await new ChatMessageRepository(test.Database).ListBySessionAsync(session.Id, CancellationToken.None));
    }

    private static ChatAttachment Attachment(Guid sessionId) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        RelativePath = $"images/{Guid.NewGuid():N}.png",
        MediaType = "image/png",
        CreatedAt = TestData.BaseTime,
    };

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
    public async Task AddAsync_ErrorKind_RoundTripsAndMissingCodeStaysNull()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var session = TestData.Session();
        await new SessionRepository(test.Database).AddAsync(session, ct);
        var repository = new ChatMessageRepository(test.Database);
        var coded = TestData.Message(session.Id, TestData.BaseTime.AddSeconds(1), "");
        coded.Role = ChatRole.Assistant;
        coded.IsError = true;
        coded.ErrorKind = ErrorKind.RateLimited;
        var legacy = TestData.Message(session.Id, TestData.BaseTime.AddSeconds(2), "Assistant error (Network): old text");
        legacy.Role = ChatRole.Assistant;
        legacy.IsError = true;

        await repository.AddAsync(coded, ct);
        await repository.AddAsync(legacy, ct);
        var list = await repository.ListBySessionAsync(session.Id, ct);

        Assert.Equal(ErrorKind.RateLimited, list[0].ErrorKind);
        Assert.Null(list[1].ErrorKind);
        Assert.Equal("Assistant error (Network): old text", list[1].Text);
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
