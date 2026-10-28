using Avalonia.Logging;
using ImageGen.Avalonia.Services.Logging;
using ImageGen.Avalonia.Tests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Tests.Services.Logging;

public class AvaloniaLogSinkTests
{
    [Theory]
    [InlineData(LogEventLevel.Information, false)]
    [InlineData(LogEventLevel.Warning, true)]
    [InlineData(LogEventLevel.Error, true)]
    public void IsEnabled_LevelFromWarning_OnlyWarningAndAboveAreKept(LogEventLevel level, bool expected)
    {
        var sink = new AvaloniaLogSink(new CapturingLogger<AvaloniaLogSink>());

        Assert.Equal(expected, sink.IsEnabled(level, "Layout"));
    }

    [Fact]
    public void Log_TemplateWithValues_WritesRenderedTextWithArea()
    {
        var logger = new CapturingLogger<AvaloniaLogSink>();
        var sink = new AvaloniaLogSink(logger);

        sink.Log(LogEventLevel.Warning, "Layout", null, "Could not find {Name} in {Scope}.", ["PageArea", "MainWindow"]);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Layout", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Could not find PageArea in MainWindow.", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_ErrorLevel_MapsToLogError()
    {
        var logger = new CapturingLogger<AvaloniaLogSink>();
        var sink = new AvaloniaLogSink(logger);

        sink.Log(LogEventLevel.Error, "Platform", null, "Backend failed");

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
    }
}
