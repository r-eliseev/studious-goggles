using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace ImageGen.Presentation.Logging;

/// <summary>
/// Writes the log to files in <c>{DataDirectory}/logs</c>: one file per day (<c>imagegen-yyyyMMdd.log</c>), 14 days kept,
/// a new file when one reaches 10 MB. This is the only place that knows the logging library; the apps only call it.
/// </summary>
public static class ImageGenLogging
{
    public const string LogsFolder = "logs";
    public const int RetainedFiles = 14;
    public const long FileSizeLimitBytes = 10 * 1024 * 1024;
    private const string FilePattern = "imagegen-.log";
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Scope} {Message:l}{NewLine}{Exception}";

    /// <summary>Folder for the log files of an app whose data folder is <paramref name="dataDirectory"/>.</summary>
    public static string LogDirectoryFor(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        return Path.Combine(dataDirectory, LogsFolder);
    }

    /// <summary>Adds the file log at <paramref name="level"/>. The file sink is closed when the container of the app is disposed.</summary>
    public static ILoggingBuilder AddImageGenFileLogging(this ILoggingBuilder builder, string logDirectory, LogLevel level)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(ToSerilog(level))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, FilePattern),
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: FileSizeLimitBytes,
                retainedFileCountLimit: RetainedFiles)
            .CreateLogger();

        builder.AddSerilog(logger, dispose: true);
        return builder;
    }

    private static LogEventLevel ToSerilog(LogLevel level) => level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        _ => LogEventLevel.Fatal,
    };
}
