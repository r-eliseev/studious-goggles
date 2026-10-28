using ImageGen.Presentation.Logging;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Tests.Logging;

public class LogLevelSettingsTests
{
    [Fact]
    public void Load_ReadsLoggingJsonFromDataFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, LogLevelSettings.FileName), "{\"level\":\"Error\"}");

            var choice = LogLevelSettings.Load(directory, environmentValue: null, LogLevel.Information);

            Assert.Equal(LogLevel.Error, choice.Level);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Resolve_NothingSet_UsesBuildDefault()
    {
        var choice = LogLevelSettings.Resolve(null, null, LogLevel.Information);

        Assert.Equal(LogLevel.Information, choice.Level);
        Assert.Equal("build default", choice.Source);
        Assert.Null(choice.Warning);
    }

    [Fact]
    public void Resolve_FileSetsLevel_UsesFileValue()
    {
        var choice = LogLevelSettings.Resolve(null, "{\"level\":\"Warning\"}", LogLevel.Information);

        Assert.Equal(LogLevel.Warning, choice.Level);
        Assert.Equal(LogLevelSettings.FileName, choice.Source);
    }

    [Fact]
    public void Resolve_EnvironmentOverridesFile()
    {
        var choice = LogLevelSettings.Resolve("trace", "{\"level\":\"Warning\"}", LogLevel.Information);

        Assert.Equal(LogLevel.Trace, choice.Level);
        Assert.Equal(LogLevelSettings.EnvironmentVariable, choice.Source);
    }

    [Fact]
    public void Resolve_UnknownEnvironmentValue_UsesBuildDefaultAndWarns()
    {
        var choice = LogLevelSettings.Resolve("loud", null, LogLevel.Debug);

        Assert.Equal(LogLevel.Debug, choice.Level);
        Assert.NotNull(choice.Warning);
        Assert.Contains("loud", choice.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_BrokenFileJson_UsesBuildDefaultAndWarns()
    {
        var choice = LogLevelSettings.Resolve(null, "{not json", LogLevel.Information);

        Assert.Equal(LogLevel.Information, choice.Level);
        Assert.NotNull(choice.Warning);
    }
}
