using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Platform;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Logging;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Platform;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AppTaskExtensions = ImageGen.Presentation.Utilities.TaskExtensions;

namespace ImageGen.Avalonia;

public sealed class MobileApp : global::Avalonia.Application
{
    private static readonly LogLevel DefaultLogLevel =
#if DEBUG
        LogLevel.Debug;
#else
        LogLevel.Information;
#endif

    public static ServiceProvider Services { get; private set; } = null!;

    public static bool Started { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var paths = new AndroidAppPaths();
        var secrets = new AndroidSecretStore(paths.DataDirectory);
        var logDirectory = ImageGenLogging.LogDirectoryFor(paths.DataDirectory);
        var level = LogLevelSettings.Load(
            paths.DataDirectory,
            Environment.GetEnvironmentVariable(LogLevelSettings.EnvironmentVariable),
            DefaultLogLevel);
        Services = AppServices.CreateShared(
            paths,
            secrets,
            secrets,
            RegisterAndroid,
            configureLogging: logging => logging.AddImageGenFileLogging(logDirectory, level.Level),
            debugTrace: DebugTraceSettings.Create(
                paths.DataDirectory,
                Environment.GetEnvironmentVariable(DebugTraceSettings.EnvironmentVariable),
                TimeProvider.System));

        secrets.AttachLogger(Services.GetRequiredService<ILogger<AndroidSecretStore>>());
        var logger = Services.GetRequiredService<ILogger<MobileApp>>();
        StartupLog.Write(logger, "Avalonia Android", paths.DataDirectory, logDirectory, level.Level);
        logger.LogInformation(
            "Android temporary directory is {TemporaryDirectory}; application cache directory is {CacheDirectory}.",
            Path.GetFullPath(Path.GetTempPath()),
            Path.GetFullPath(global::Android.App.Application.Context.CacheDir!.AbsolutePath));
        if (level.Warning is not null)
        {
            logger.LogWarning("{Warning}", level.Warning);
        }

        CrashLog.Attach(logger);
        Logger.Sink = new AvaloniaLogSink(logger);
        Dispatcher.UIThread.UnhandledException += (_, args) =>
            logger.LogCritical(args.Exception, "Unhandled exception on the UI thread.");
        AppTaskExtensions.Logger = Services.GetRequiredService<ILoggerFactory>().CreateLogger("ImageGen.Background");

        new AvaloniaAppearanceApplier(Services.GetRequiredService<AppearanceService>(), this).Start();
        if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = () => new MainView();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterAndroid(IServiceCollection services)
    {
        services.AddSingleton<OverlayDialogService>(sp => new OverlayDialogService(
            () => MainView.Current?.OverlayHost,
            sp.GetRequiredService<ILogger<OverlayDialogService>>()));
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<OverlayDialogService>());
        services.AddSingleton<IFilePickerService>(new AvaloniaFilePickerService(() => TopLevel.GetTopLevel(MainView.Current)));
        services.AddSingleton<IClipboardService>(new AvaloniaClipboardService(() => TopLevel.GetTopLevel(MainView.Current)));
        services.AddSingleton<IImageSaver, AndroidImageSaver>();
        services.AddSingleton<IFolderLauncher, UnsupportedFolderLauncher>();
        services.AddSingleton<ILogSharer, AndroidLogSharer>();
        services.AddSingleton<OverlayImageViewerService>(sp => new OverlayImageViewerService(
            () => MainView.Current?.OverlayHost,
            sp.GetRequiredService<IDialogService>()));
        services.AddSingleton<IImageViewerService>(sp => sp.GetRequiredService<OverlayImageViewerService>());
        services.AddSingleton<OverlayImageComparisonService>(sp => new OverlayImageComparisonService(
            () => MainView.Current?.OverlayHost,
            sp.GetRequiredService<IDialogService>()));
        services.AddSingleton<IImageComparisonService>(sp => sp.GetRequiredService<OverlayImageComparisonService>());
    }
}
