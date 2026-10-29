using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Platform;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Logging;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AppTaskExtensions = ImageGen.Presentation.Utilities.TaskExtensions;

namespace ImageGen.Avalonia;

public partial class App : Application
{
    /// <summary>Composition root of the running app, set when the framework is initialized.</summary>
    public ServiceProvider? Services { get; private set; }

    /// <summary>Data folder and key store instead of the real ones. Tests set it before the app starts; the real app leaves it null.</summary>
    public static AppHostOverrides? HostOverrides { get; set; }

    /// <summary>The main window, created at startup whether or not the platform has a desktop lifetime.</summary>
    public MainWindow? MainWindow { get; private set; }

    /// <summary>Debug builds log more by default; <c>IMAGEGEN_LOG_LEVEL</c> and <c>logging.json</c> change it.</summary>
    private static readonly LogLevel DefaultLogLevel =
#if DEBUG
        LogLevel.Debug;
#else
        LogLevel.Information;
#endif

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var paths = HostOverrides?.Paths ?? new DesktopAppPaths();
        var secrets = HostOverrides?.Secrets ?? DesktopSecretStore.ForCurrentPlatform(paths);
        var logDirectory = ImageGenLogging.LogDirectoryFor(paths.DataDirectory);
        var level = LogLevelSettings.Load(
            paths.DataDirectory,
            Environment.GetEnvironmentVariable(LogLevelSettings.EnvironmentVariable),
            DefaultLogLevel);
        Services = AppServices.Create(
            paths,
            secrets,
            secrets,
            configureLogging: logging => logging.AddImageGenFileLogging(logDirectory, level.Level));

        var logger = Services.GetRequiredService<ILogger<App>>();
        StartupLog.Write(logger, "Avalonia", paths.DataDirectory, logDirectory, level.Level);
        if (level.Warning is not null)
        {
            logger.LogWarning("{Warning}", level.Warning);
        }

        CrashLog.Attach(logger);
        // Replaces the trace sink of Program: Avalonia's own warnings now go to the same file.
        Logger.Sink = new AvaloniaLogSink(logger);
        Dispatcher.UIThread.UnhandledException += (_, args) =>
            logger.LogCritical(args.Exception, "Unhandled exception on the UI thread.");
        AppTaskExtensions.Logger = Services.GetRequiredService<ILoggerFactory>().CreateLogger("ImageGen.Background");

        new AvaloniaAppearanceApplier(Services.GetRequiredService<AppearanceService>(), this).Start();
        MainWindow = new MainWindow();
        MainWindow.ConfigureShortcuts(Services.GetRequiredService<SessionsPageModel>(), Services.GetRequiredService<AvaloniaNavigationService>());

        var navigation = Services.GetRequiredService<AvaloniaNavigationService>();
        navigation.AttachHost(MainWindow.PageArea);
        navigation.GoToAsync(Routes.Sessions).FireAndForgetSafeAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = MainWindow;
            // Disposing the container closes the log file, so the last lines reach the disk.
            desktop.Exit += (_, _) => Services?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
