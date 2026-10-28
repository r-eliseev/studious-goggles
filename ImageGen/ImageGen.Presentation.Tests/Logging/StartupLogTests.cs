using ImageGen.Presentation.Logging;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Tests.Logging;

public class StartupLogTests
{
    [Fact]
    public void Write_LogsShellDataFolderLogFolderAndLevel()
    {
        var logger = new CapturingLogger<StartupLogTests>();

        StartupLog.Write(logger, "Avalonia", "/data/app", "/data/app/logs", LogLevel.Debug);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("Avalonia", entry.Message, StringComparison.Ordinal);
        Assert.Contains("/data/app/logs", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Debug", entry.Message, StringComparison.Ordinal);
    }
}
