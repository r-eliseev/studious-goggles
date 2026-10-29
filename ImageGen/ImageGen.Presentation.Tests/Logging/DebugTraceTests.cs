using System.Text.Json;
using ImageGen.Core.Diagnostics;
using ImageGen.Presentation.Logging;

namespace ImageGen.Presentation.Tests.Logging;

public class DebugTraceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("1", true)]
    [InlineData("TRUE", true)]
    [InlineData("on", true)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    public void Resolve_EnvironmentSet_UsesIt(string value, bool expected)
    {
        var fileSaysTheOpposite = expected ? "{\"debugTrace\":false}" : "{\"debugTrace\":true}";

        Assert.Equal(expected, DebugTraceSettings.Resolve(value, fileSaysTheOpposite));
    }

    [Fact]
    public void Resolve_EnvironmentOffOverridesFile()
    {
        Assert.False(DebugTraceSettings.Resolve("0", "{\"debugTrace\":true}"));
    }

    [Fact]
    public void Resolve_FileTrue_On()
    {
        Assert.True(DebugTraceSettings.Resolve(null, "{\"debugTrace\":true}"));
    }

    [Fact]
    public void Resolve_NothingSet_Off()
    {
        Assert.False(DebugTraceSettings.Resolve(null, null));
        Assert.False(DebugTraceSettings.Resolve(null, "{\"level\":\"Debug\"}"));
    }

    [Fact]
    public void Resolve_InvalidFile_Off()
    {
        Assert.False(DebugTraceSettings.Resolve(null, "{not json"));
    }

    [Fact]
    public void Create_Off_ReturnsNullTrace()
    {
        var directory = NewDirectory();
        try
        {
            var trace = DebugTraceSettings.Create(directory, null, new FixedClock(Now));

            Assert.IsType<NullDebugTrace>(trace);
            Assert.False(Directory.Exists(Path.Combine(directory, "debug")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Record_UserInputAndPicture_WritesLineAndBlobFile()
    {
        var directory = NewDirectory();
        try
        {
            var trace = new FileDebugTrace(directory, new FixedClock(Now));
            var picture = new byte[] { 137, 80, 78, 71 };

            trace.Record(new TraceRecord("user-input", new Dictionary<string, string?> { ["text"] = "fox" }, []));
            trace.Record(new TraceRecord(
                "http-response",
                new Dictionary<string, string?> { ["body"] = "blob:r-0" },
                [new TraceBlob("r-0", picture, "image/png")]));

            var dayDirectory = Path.Combine(directory, "debug", "2026-10-29");
            var lines = File.ReadAllLines(Path.Combine(dayDirectory, "trace.jsonl"));
            Assert.Equal(2, lines.Length);

            using var first = JsonDocument.Parse(lines[0]);
            Assert.Equal(FileDebugTrace.Schema, first.RootElement.GetProperty("schema").GetString());
            Assert.Equal("user-input", first.RootElement.GetProperty("kind").GetString());
            Assert.Equal("fox", first.RootElement.GetProperty("fields").GetProperty("text").GetString());

            using var second = JsonDocument.Parse(lines[1]);
            var blob = second.RootElement.GetProperty("blobs")[0];
            Assert.Equal("r-0.png", blob.GetProperty("file").GetString());
            Assert.Equal(picture, File.ReadAllBytes(Path.Combine(dayDirectory, "r-0.png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
