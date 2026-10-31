using System.Reflection;
using ImageGen.Core.Platform;
using ImageGen.Core.Storage;
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
    public void GetFullPath_StoredRelativePath_UsesSeparatorsOfTheSystem()
    {
        // The database keeps "/" on every platform; the full path goes to platform image loaders.
        // WinRT StorageFile.GetFileFromPathAsync (MAUI on Windows) does not find a file whose path contains "/".
        var full = _store.GetFullPath("images/abc/def.jpg");

        Assert.Equal(Path.Combine(_directory, "images", "abc", "def.jpg"), full);
        if (OperatingSystem.IsWindows())
        {
            Assert.DoesNotContain('/', full);
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

    [Theory]
    [InlineData("dimensions.jpg", "image/jpeg", 800, 600)]
    [InlineData("dimensions.png", "image/png", 8, 12)]
    [InlineData("dimensions.webp", "image/webp", 16, 16)]
    [InlineData("dimensions.gif", "image/gif", 10, 10)]
    public async Task TryReadDimensionsAsync_ValidImage_ReturnsHeaderDimensions(string fixture, string mediaType, int width, int height)
    {
        var bytes = ReadFixture(fixture);
        var path = await _store.SaveAsync(Guid.NewGuid(), Guid.NewGuid(), bytes, mediaType, TestContext.Current.CancellationToken);

        var dimensions = await _store.TryReadDimensionsAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(new ImageDimensions(width, height), dimensions);
        Assert.Equal(bytes, await _store.ReadAsync(path, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("random.bin", new byte[] { 1, 2, 3, 4, 5 })]
    [InlineData("truncated.png", new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })]
    public async Task TryReadDimensionsAsync_CorruptOrUnsupported_ReturnsNull(string name, byte[] bytes)
    {
        var path = $"images/{Guid.NewGuid():N}/{name}";
        Directory.CreateDirectory(Path.GetDirectoryName(_store.GetFullPath(path))!);
        await File.WriteAllBytesAsync(_store.GetFullPath(path), bytes, TestContext.Current.CancellationToken);

        var dimensions = await _store.TryReadDimensionsAsync(path, TestContext.Current.CancellationToken);

        Assert.Null(dimensions);
    }

    [Fact]
    public async Task TryReadDimensionsAsync_MissingFile_ReturnsNull()
    {
        var dimensions = await _store.TryReadDimensionsAsync("images/missing/no-image.png", TestContext.Current.CancellationToken);

        Assert.Null(dimensions);
    }

    [Fact]
    public async Task TryReadDimensionsAsync_PreCancelled_ThrowsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _store.TryReadDimensionsAsync("images/missing/no-image.png", cancellation.Token));
    }

    private static byte[] ReadFixture(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"TestData/{name}");
        Assert.NotNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
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

    [Fact]
    public async Task SaveAsync_ExistingImage_AtomicallyReplacesBytes()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessionId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var relative = await _store.SaveAsync(sessionId, generationId, [1], "image/png", ct);

        var overwritten = await _store.SaveAsync(sessionId, generationId, [2, 3], "image/png", ct);

        Assert.Equal(relative, overwritten);
        Assert.Equal(new byte[] { 2, 3 }, await File.ReadAllBytesAsync(_store.GetFullPath(relative), ct));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_store.GetFullPath(relative))!, "*.tmp"));
    }

    [Fact]
    public async Task SaveAttachmentAsync_ExistingAttachment_AtomicallyReplacesBytes()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessionId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var relative = await _store.SaveAttachmentAsync(sessionId, attachmentId, [1], "image/png", ct);

        var overwritten = await _store.SaveAttachmentAsync(sessionId, attachmentId, [4, 5], "image/png", ct);

        Assert.Equal(relative, overwritten);
        Assert.Equal(new byte[] { 4, 5 }, await File.ReadAllBytesAsync(_store.GetFullPath(relative), ct));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_store.GetFullPath(relative))!, "*.tmp"));
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
