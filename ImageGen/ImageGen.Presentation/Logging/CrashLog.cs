using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Logging;

/// <summary>Writes exceptions that nobody handled: on any thread, and on background tasks that nobody awaited.</summary>
public static class CrashLog
{
    private static int _attached;

    /// <summary>Subscribes once; later calls do nothing. UI-thread exceptions are attached by each shell.</summary>
    public static void Attach(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (Interlocked.Exchange(ref _attached, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            logger.LogCritical(args.ExceptionObject as Exception, "Unhandled exception. Terminating: {IsTerminating}.", args.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogError(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };
    }
}
