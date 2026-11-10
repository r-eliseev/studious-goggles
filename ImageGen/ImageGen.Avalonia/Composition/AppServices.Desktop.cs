using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using ImageGen.Avalonia.Services;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Platform;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Composition;

public static partial class AppServices
{
    /// <summary>
    /// Builds the container of the desktop app: core, storage, the shared presentation layer, the Avalonia pages and the
    /// platform services. Validates the registrations at build time.
    /// <paramref name="openRouterHandler"/> and <paramref name="dialogs"/> replace the network and dialog windows
    /// in tests; the app itself passes neither. <paramref name="configureLogging"/> adds log providers (the app adds the file log).
    /// </summary>
    public static ServiceProvider Create(
        IAppPaths paths,
        ISecretStore secrets,
        ISecretPersistence persistence,
        HttpMessageHandler? openRouterHandler = null,
        IDialogService? dialogs = null,
        Action<ILoggingBuilder>? configureLogging = null,
        IDebugTrace? debugTrace = null) =>
        CreateShared(paths, secrets, persistence, services => RegisterDesktop(services, dialogs), openRouterHandler, configureLogging, debugTrace);

    private static void RegisterDesktop(IServiceCollection services, IDialogService? dialogs)
    {
        services.AddSingleton<IDialogService>(dialogs ?? new AvaloniaDialogService(MainWindowOwner));
        services.AddSingleton<IFilePickerService>(new AvaloniaFilePickerService(MainWindowOwner));
        services.AddSingleton<IClipboardService>(new AvaloniaClipboardService(MainWindowOwner));
        services.AddSingleton<IImageSaver>(new AvaloniaImageSaver(MainWindowOwner));
        services.AddSingleton<IMediaPlayerService>(sp => new AvaloniaMediaPlayerService(
            MainWindowOwner,
            new LibVlcMediaEngine(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("ImageGen.Avalonia.MediaPlayer")));
        services.AddSingleton<IVideoPosterService, UnsupportedVideoPosterService>();
        services.AddSingleton<AvaloniaFullScreenController>();
        services.AddSingleton<IImageViewerService>(sp => new AvaloniaImageViewerService(
            MainWindowOwner,
            sp.GetRequiredService<AvaloniaFullScreenController>(),
            sp.GetRequiredService<IDialogService>()));
        services.AddSingleton<IImageComparisonService>(sp => new AvaloniaImageComparisonService(MainWindowOwner, sp.GetRequiredService<IDialogService>()));
        services.AddSingleton<IFolderLauncher>(new AvaloniaFolderLauncher(MainWindowOwner));
        services.AddSingleton<ILogSharer>(new UnsupportedLogSharer());
    }

    private static Window? MainWindowOwner() =>
        Application.Current is App app ? app.MainWindow : null;
}
