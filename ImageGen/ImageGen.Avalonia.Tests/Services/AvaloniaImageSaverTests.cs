using ImageGen.Avalonia.Services;

namespace ImageGen.Avalonia.Tests.Services;

public class AvaloniaImageSaverTests
{
    [Fact]
    public async Task CopyAsync_WritesAllBytesToTarget()
    {
        var picture = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
        using var source = new MemoryStream(picture);
        using var target = new MemoryStream();

        await AvaloniaImageSaver.CopyAsync(source, target, TestContext.Current.CancellationToken);

        Assert.Equal(picture, target.ToArray());
    }
}
