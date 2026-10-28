using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Logging;

/// <summary>
/// Chooses the log level without restarting the build: the environment variable <c>IMAGEGEN_LOG_LEVEL</c> first,
/// then the <c>level</c> property of <c>logging.json</c> in the data folder, then the default of the build.
/// An unknown value is ignored and reported in <see cref="LogLevelChoice.Warning"/>.
/// </summary>
public static class LogLevelSettings
{
    public const string EnvironmentVariable = "IMAGEGEN_LOG_LEVEL";
    public const string FileName = "logging.json";

    /// <summary>Reads <c>logging.json</c> from <paramref name="dataDirectory"/> (if it exists) and resolves the level.</summary>
    public static LogLevelChoice Load(string dataDirectory, string? environmentValue, LogLevel buildDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var path = Path.Combine(dataDirectory, FileName);
        var fileText = File.Exists(path) ? File.ReadAllText(path) : null;
        return Resolve(environmentValue, fileText, buildDefault);
    }

    public static LogLevelChoice Resolve(string? environmentValue, string? fileText, LogLevel buildDefault)
    {
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return Parse(environmentValue, EnvironmentVariable, buildDefault);
        }

        if (!string.IsNullOrWhiteSpace(fileText))
        {
            return FromFile(fileText, buildDefault);
        }

        return new LogLevelChoice(buildDefault, "build default", null);
    }

    private static LogLevelChoice FromFile(string text, LogLevel buildDefault)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("level", out var value) && value.ValueKind == JsonValueKind.String)
            {
                return Parse(value.GetString() ?? string.Empty, FileName, buildDefault);
            }

            return new LogLevelChoice(buildDefault, "build default", null);
        }
        catch (JsonException ex)
        {
            return new LogLevelChoice(buildDefault, "build default", $"{FileName} is not valid JSON ({ex.Message}); using the build default.");
        }
    }

    private static LogLevelChoice Parse(string value, string source, LogLevel fallback)
    {
        if (Enum.TryParse<LogLevel>(value.Trim(), ignoreCase: true, out var level) && Enum.IsDefined(level))
        {
            return new LogLevelChoice(level, source, null);
        }

        return new LogLevelChoice(fallback, "build default", $"Unknown log level '{value}' in {source}; using the build default.");
    }
}
