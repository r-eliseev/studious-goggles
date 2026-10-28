using System.Text.RegularExpressions;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Services.Logging;

/// <summary>
/// Sends Avalonia's own warnings and errors (layout, styles, platform backend) to the app log, so that they land in the
/// same file as the app's messages. Information and below are dropped: Avalonia logs too much to keep at that level.
/// </summary>
public sealed partial class AvaloniaLogSink(ILogger logger) : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
        Write(level, area, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, object?[] propertyValues) =>
        Write(level, area, messageTemplate, propertyValues);

    private void Write(LogEventLevel level, string area, string messageTemplate, object?[] values)
    {
        // Avalonia templates use {Name} placeholders filled in order; the source object is not logged.
        var index = 0;
        var message = Placeholder().Replace(messageTemplate, match =>
            index < values.Length ? Convert.ToString(values[index++], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty : match.Value);
        logger.Log(ToMicrosoft(level), "Avalonia {Area}: {Message}", area, message);
    }

    private static LogLevel ToMicrosoft(LogEventLevel level) => level switch
    {
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Critical,
        _ => LogLevel.Information,
    };

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Placeholder();
}
