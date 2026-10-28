using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;

namespace ImageGen.Core.Tests.Services;

public class AttachmentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.Parse("3f1a2b3c-4d5e-4f60-8a7b-9c0d1e2f3a4b");

    private readonly IChatAttachmentRepository _repository = Substitute.For<IChatAttachmentRepository>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly AttachmentService _service;

    public AttachmentServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _files.SaveAttachmentAsync(SessionId, Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => $"images/{SessionId:N}/attachments/{ci.ArgAt<Guid>(1):N}.png");
        _service = new AttachmentService(_repository, _files, time);
    }

    [Fact]
    public async Task AddImageAsync_Png_SavesFileAndRecord()
    {
        var attachment = await _service.AddImageAsync(SessionId, [1, 2, 3], "image/png", TestContext.Current.CancellationToken);

        Assert.Equal(SessionId, attachment.SessionId);
        Assert.Equal("image/png", attachment.MediaType);
        Assert.Null(attachment.MessageId);
        Assert.Equal(Now, attachment.CreatedAt);
        await _files.Received(1).SaveAttachmentAsync(SessionId, attachment.Id, Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(attachment, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/jpg")]
    [InlineData("text/plain")]
    public async Task AddImageAsync_UnsupportedType_ThrowsAndWritesNothing(string mediaType)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddImageAsync(SessionId, [1], mediaType, TestContext.Current.CancellationToken));

        await _files.DidNotReceive().SaveAttachmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<ChatAttachment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddImageAsync_Empty_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddImageAsync(SessionId, [], "image/png", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddImageAsync_LargerThan10MB_Throws()
    {
        var content = new byte[AttachmentService.MaxAttachmentBytes + 1];

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddImageAsync(SessionId, content, "image/png", TestContext.Current.CancellationToken));

        await _files.DidNotReceive().SaveAttachmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddImageAsync_RecordFails_DeletesTheFile()
    {
        _repository.AddAsync(Arg.Any<ChatAttachment>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("disk full")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AddImageAsync(SessionId, [1], "image/png", TestContext.Current.CancellationToken));

        await _files.Received(1).DeleteFileAsync(Arg.Is<string>(p => p.EndsWith(".png", StringComparison.Ordinal)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadDataUrlAsync_ReturnsDataUrlOfStoredFile()
    {
        var attachment = new ChatAttachment
        {
            Id = Guid.NewGuid(),
            SessionId = SessionId,
            RelativePath = "images/x/attachments/y.png",
            MediaType = "image/png",
            CreatedAt = Now,
        };
        _repository.GetAsync(attachment.Id, Arg.Any<CancellationToken>()).Returns(attachment);
        _files.ReadAsync(attachment.RelativePath, Arg.Any<CancellationToken>()).Returns([1, 2, 3]);

        var url = await _service.ReadDataUrlAsync(attachment.Id, TestContext.Current.CancellationToken);

        Assert.Equal("data:image/png;base64,AQID", url);
    }
}
