using System.Text.Json;
using ImageGen.Core.Diagnostics;

namespace ImageGen.Presentation.Logging;

/// <summary>
/// Decides whether the debug trace (task 085) is written. Off by default. The environment variable
/// <c>IMAGEGEN_DEBUG_TRACE</c> wins over the <c>debugTrace</c> property of <c>logging.json</c> in the data folder.
/// Any value other than true, 1, on or yes counts as off.
/// </summary>
public static class DebugTraceSettings
{
    public const string EnvironmentVariable = "IMAGEGEN_DEBUG_TRACE";
    public const string FileProperty = "debugTrace";

    public static bool Resolve(string? environmentValue, string? logSettingsText)
    {
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return IsOn(environmentValue);
        }

        if (string.IsNullOrWhiteSpace(logSettingsText))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(logSettingsText);
            return document.RootElement.TryGetProperty(FileProperty, out var value)
                && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.String && IsOn(value.GetString()!));
        }
        catch (JsonException)
        {
            // An invalid file is reported by LogLevelSettings; the trace stays off here.
            return false;
        }
    }

    /// <summary>The trace of this run: the file trace in the data folder when it is on, otherwise nothing.</summary>
    public static IDebugTrace Create(string dataDirectory, string? environmentValue, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var path = Path.Combine(dataDirectory, LogLevelSettings.FileName);
        var fileText = File.Exists(path) ? File.ReadAllText(path) : null;
        return Resolve(environmentValue, fileText)
            ? new FileDebugTrace(dataDirectory, timeProvider)
            : NullDebugTrace.Instance;
    }

    private static bool IsOn(string? value) =>
        value?.Trim().ToLowerInvariant() is "true" or "1" or "on" or "yes";
}
