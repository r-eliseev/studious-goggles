using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Logging;

/// <summary>The first line of every run: enough to tell which build, shell and machine wrote the log.</summary>
public static class StartupLog
{
    public static void Write(ILogger logger, string shell, string dataDirectory, string logDirectory, LogLevel level)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogInformation(
            "ImageGen {Version} starting in the {Shell} shell on {OperatingSystem} {Architecture} ({Runtime}). Data: {DataDirectory}. Logs: {LogDirectory}. Level: {Level}.",
            Version(),
            shell,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture,
            RuntimeInformation.FrameworkDescription,
            dataDirectory,
            logDirectory,
            level);
    }

    private static string Version()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(StartupLog).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
