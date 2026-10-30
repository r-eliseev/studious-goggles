using CommunityToolkit.Maui;
using ImageGen.Core;
using ImageGen.Core.Platform;
using ImageGen.Data;
using ImageGen.Maui.Pages;
using ImageGen.Maui.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AppTaskExtensions = ImageGen.Presentation.Utilities.TaskExtensions;

namespace ImageGen.Maui;

public static class MauiProgram
{
    /// <summary>Debug builds log more by default; <c>IMAGEGEN_LOG_LEVEL</c> and <c>logging.json</c> change it.</summary>
    private static readonly LogLevel DefaultLogLevel =
#if DEBUG
        LogLevel.Debug;
#else
        LogLevel.Information;
#endif

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Onest-Regular.ttf", "OnestRegular");
                fonts.AddFont("Onest-Medium.ttf", "OnestMedium");
                fonts.AddFont("Onest-SemiBold.ttf", "OnestSemiBold");
                fonts.AddFont("IBMPlexSans-Regular.ttf", "PlexSansRegular");
                fonts.AddFont("IBMPlexSans-Medium.ttf", "PlexSansMedium");
                fonts.AddFont("IBMPlexSans-SemiBold.ttf", "PlexSansSemiBold");
                fonts.AddFont("IBMPlexMono-Regular.ttf", "PlexMonoRegular");
                fonts.AddFont("IBMPlexMono-Medium.ttf", "PlexMonoMedium");
                fonts.AddFont("lucide.ttf", "LucideIcons");
            });

#if WINDOWS
        ImageGen.Maui.Platforms.Windows.ChatEditorKeyHandling.Register();
#endif

#if ANDROID
        ImageGen.Maui.Platforms.Android.ChatEditorImeHandling.Register();
#endif

        var paths = new MauiAppPaths();
        var logDirectory = ImageGenLogging.LogDirectoryFor(paths.DataDirectory);
        var level = LogLevelSettings.Load(
            paths.DataDirectory,
            Environment.GetEnvironmentVariable(LogLevelSettings.EnvironmentVariable),
            DefaultLogLevel);
        builder.Logging.AddDebug();
        builder.Logging.AddImageGenFileLogging(logDirectory, level.Level);

        builder.Services.AddSingleton<IAppPaths>(paths);
        builder.Services.AddSingleton<ISecretStore, MauiSecretStore>();
        builder.Services.AddSingleton<ISecretPersistence, MauiSecretPersistence>();
        builder.Services.AddSingleton(DebugTraceSettings.Create(
            paths.DataDirectory,
            Environment.GetEnvironmentVariable(DebugTraceSettings.EnvironmentVariable),
            TimeProvider.System));
        builder.Services.AddImageGenCore();
        builder.Services.AddImageGenData();
        builder.Services.AddSingleton<AppInitializer>();
        builder.Services.AddSingleton<IDialogService, ShellDialogService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IMainThread, MauiMainThread>();
        builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();
        builder.Services.AddSingleton<AppearanceService>();
        builder.Services.AddSingleton<MauiAppearanceApplier>();
        builder.Services.AddSingleton<IClipboardService, MauiClipboardService>();
        builder.Services.AddSingleton<IImageSaver, MauiImageSaver>();
        builder.Services.AddSingleton<IImageViewerService, MauiImageViewerService>();
        builder.Services.AddSingleton<ICurrentSessionTracker, CurrentSessionTracker>();

        // Pages and page models
        builder.Services.AddSingleton<SessionsPageModel>();
        builder.Services.AddSingleton<SessionsPage>();
        builder.Services.AddTransientWithShellRoute<ApiKeyPage, ApiKeyPageModel>(Routes.ApiKey);
        builder.Services.AddTransientWithShellRoute<ChatPage, ChatPageModel>(Routes.Chat);
        builder.Services.AddTransientWithShellRoute<SessionModelsPage, SessionModelsPageModel>(Routes.SessionModels);
        builder.Services.AddTransientWithShellRoute<SessionParametersPage, SessionParametersPageModel>(Routes.SessionParameters);
        builder.Services.AddTransientWithShellRoute<ModelPickerPage, ModelPickerPageModel>(Routes.ModelPicker);
        builder.Services.AddTransientWithShellRoute<SettingsPage, SettingsPageModel>(Routes.Settings);
        // The chat side pane (wide window) creates a fresh page model each time it opens.
        builder.Services.AddSingleton<Func<SessionModelsPageModel>>(sp => sp.GetRequiredService<SessionModelsPageModel>);
        builder.Services.AddSingleton<Func<SessionParametersPageModel>>(sp => sp.GetRequiredService<SessionParametersPageModel>);
        builder.Services.AddSingleton<IFolderLauncher, MauiFolderLauncher>();

        var app = builder.Build();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ImageGen.Startup");
        StartupLog.Write(logger, "MAUI", paths.DataDirectory, logDirectory, level.Level);
        if (level.Warning is not null)
        {
            logger.LogWarning("{Warning}", level.Warning);
        }

        CrashLog.Attach(logger);
        AppTaskExtensions.Logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ImageGen.Background");
        return app;
    }
}
