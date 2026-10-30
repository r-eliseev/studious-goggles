using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Data.Repositories;

namespace ImageGen.Data.Tests;

public class ChatAttachmentRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsync_ThenGet_ReturnsSameAttachment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessionId = await CreateSessionAsync(test, ct);
        var repository = new ChatAttachmentRepository(test.Database);
        var attachment = Attachment(sessionId, "images/a/attachments/b.png");

        await repository.AddAsync(attachment, ct);

        var stored = await repository.GetAsync(attachment.Id, ct);
        Assert.NotNull(stored);
        Assert.Equal("images/a/attachments/b.png", stored.RelativePath);
        Assert.Equal("image/png", stored.MediaType);
        Assert.Null(stored.MessageId);
        Assert.Equal(Now, stored.CreatedAt);
    }

    [Fact]
    public async Task AddUserWithAttachmentsAsync_ThenListByMessage_ReturnsLinkedAttachments()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessionId = await CreateSessionAsync(test, ct);
        var message = new ChatMessage { Id = Guid.NewGuid(), SessionId = sessionId, Role = ChatRole.User, Text = "x", CreatedAt = Now };
        var repository = new ChatAttachmentRepository(test.Database);
        var first = Attachment(sessionId, "images/a/1.png");
        var second = Attachment(sessionId, "images/a/2.png");
        await repository.AddAsync(first, ct);
        await repository.AddAsync(second, ct);

        await new ChatMessageRepository(test.Database).AddUserWithAttachmentsAsync(message, [first.Id, second.Id], ct);

        var linked = await repository.ListByMessageAsync(message.Id, ct);
        Assert.Equal([first.Id, second.Id], linked.Select(a => a.Id).OrderBy(id => id == first.Id ? 0 : 1));
        Assert.All(linked, a => Assert.Equal(message.Id, a.MessageId));
    }

    [Fact]
    public async Task DeleteAsync_RemovesAttachment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync(ct);
        var sessionId = await CreateSessionAsync(test, ct);
        var repository = new ChatAttachmentRepository(test.Database);
        var attachment = Attachment(sessionId, "images/a/1.png");
        await repository.AddAsync(attachment, ct);

        await repository.DeleteAsync(attachment.Id, ct);

        Assert.Null(await repository.GetAsync(attachment.Id, ct));
    }

    private static ChatAttachment Attachment(Guid sessionId, string path) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        RelativePath = path,
        MediaType = "image/png",
        CreatedAt = Now,
    };

    private static async Task<Guid> CreateSessionAsync(TestDatabase test, CancellationToken ct)
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "S",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = SessionSettings.CreateDefault(),
        };
        await new SessionRepository(test.Database).AddAsync(session, ct);
        return session.Id;
    }
}
