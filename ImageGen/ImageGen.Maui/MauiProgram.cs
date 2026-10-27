using CommunityToolkit.Maui;
using ImageGen.Core;
using ImageGen.Core.Platform;
using ImageGen.Data;
using ImageGen.Maui.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<IAppPaths, MauiAppPaths>();
        builder.Services.AddSingleton<ISecretStore, MauiSecretStore>();
        builder.Services.AddImageGenCore();
        builder.Services.AddImageGenData();
        builder.Services.AddSingleton<AppInitializer>();
        builder.Services.AddSingleton<IDialogService, ShellDialogService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();

        // Pages and page models

        return builder.Build();
    }
}
