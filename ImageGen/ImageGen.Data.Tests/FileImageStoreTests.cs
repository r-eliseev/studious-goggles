using ImageGen.Core.Platform;
using ImageGen.Data.Tests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace ImageGen.Data.Tests;

public sealed class FileImageStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
    private readonly CapturingLogger<FileImageStore> _logger = new();
    private readonly FileImageStore _store;

    public FileImageStoreTests()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(_directory);
        _store = new FileImageStore(paths, _logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAttachmentAsync_WritesIntoSessionAttachmentFolder()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessionId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        var relativePath = await _store.SaveAttachmentAsync(sessionId, attachmentId, [5, 6], "image/webp", ct);

        Assert.Equal($"images/{sessionId:N}/attachments/{attachmentId:N}.webp", relativePath);
        Assert.Equal([5, 6], await _store.ReadAsync(relativePath, ct));
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesFileAndIgnoresMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var relativePath = await _store.SaveAttachmentAsync(Guid.NewGuid(), Guid.NewGuid(), [1], "image/png", ct);

        await _store.DeleteFileAsync(relativePath, ct);
        await _store.DeleteFileAsync(relativePath, ct);

        Assert.False(File.Exists(_store.GetFullPath(relativePath)));
    }

    [Fact]
    public async Task ReadAsync_SavedFile_ReturnsSameBytes()
    {
        var ct = TestContext.Current.CancellationToken;
        var relativePath = await _store.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), [9, 8, 7, 0], "image/png", ct);

        Assert.Equal([9, 8, 7, 0], await _store.ReadAsync(relativePath, ct));
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsFileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _store.ReadAsync("images/none/none.png", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_Png_LogsDebugWithSizeAndNoContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var generationId = Guid.NewGuid();

        await _store.SaveAsync(Guid.NewGuid(), generationId, [1, 2, 3, 4], "image/png", ct);

        var entry = Assert.Single(_logger.Entries, e => e.Message.StartsWith("Image saved", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("4 bytes", entry.Message, StringComparison.Ordinal);
        Assert.Contains(generationId.ToString("N"), entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_Png_WritesFileAndReturnsRelativePath()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessionId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        byte[] content = [1, 2, 3];

        var relative = await _store.SaveAsync(sessionId, generationId, content, "image/png", ct);

        Assert.Equal($"images/{sessionId:N}/{generationId:N}.png", relative);
        Assert.Equal(content, await File.ReadAllBytesAsync(_store.GetFullPath(relative), ct));
    }

    [Theory]
    [InlineData("image/png", ".png")]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("IMAGE/WEBP", ".webp")]
    [InlineData("application/octet-stream", ".bin")]
    public void GetExtension_MediaType_ReturnsExtension(string mediaType, string expected)
    {
        Assert.Equal(expected, FileImageStore.GetExtension(mediaType));
    }

    [Fact]
    public async Task DeleteSessionFilesAsync_ExistingFiles_DeletesFolder()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessionId = Guid.NewGuid();
        var relative = await _store.SaveAsync(sessionId, Guid.NewGuid(), [1], "image/png", ct);

        await _store.DeleteSessionFilesAsync(sessionId, ct);

        Assert.False(File.Exists(_store.GetFullPath(relative)));
    }

    [Fact]
    public async Task DeleteSessionFilesAsync_NoFiles_DoesNotThrow()
    {
        await _store.DeleteSessionFilesAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
    }
}
