using ImageGen.Core.Platform;

namespace ImageGen.Data.Tests;

public sealed class FileImageStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
    private readonly FileImageStore _store;

    public FileImageStoreTests()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(_directory);
        _store = new FileImageStore(paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
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
