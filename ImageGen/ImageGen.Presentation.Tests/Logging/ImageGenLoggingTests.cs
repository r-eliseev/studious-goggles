using ImageGen.Presentation.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Tests.Logging;

public sealed class ImageGenLoggingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void AddImageGenFileLogging_WritesDailyFileWithTheMessage()
    {
        using (var provider = Build(LogLevel.Debug))
        {
            provider.GetRequiredService<ILogger<ImageGenLoggingTests>>().LogInformation("hello {Value}", "from-test");
        }

        var file = Assert.Single(Directory.GetFiles(_directory, "imagegen-*.log"));
        Assert.Matches(@"imagegen-\d{8}\.log$", file);
        Assert.Contains("hello from-test", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void AddImageGenFileLogging_LevelInformation_DropsDebugEntries()
    {
        using (var provider = Build(LogLevel.Information))
        {
            provider.GetRequiredService<ILogger<ImageGenLoggingTests>>().LogDebug("debug-only-entry");
        }

        var text = Directory.Exists(_directory) && Directory.GetFiles(_directory, "imagegen-*.log") is [var file]
            ? File.ReadAllText(file)
            : string.Empty;
        Assert.DoesNotContain("debug-only-entry", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AddImageGenFileLogging_FileOlderThan14Days_IsDeletedAtStart()
    {
        Directory.CreateDirectory(_directory);
        var old = Path.Combine(_directory, $"imagegen-{DateTime.UtcNow.AddDays(-30):yyyyMMdd}.log");
        File.WriteAllText(old, "old");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-30));

        using (var provider = Build(LogLevel.Information))
        {
            provider.GetRequiredService<ILogger<ImageGenLoggingTests>>().LogInformation("start");
        }

        Assert.False(File.Exists(old));
    }

    private ServiceProvider Build(LogLevel level) =>
        new ServiceCollection()
            .AddLogging(builder => builder.AddImageGenFileLogging(_directory, level))
            .BuildServiceProvider();
}
