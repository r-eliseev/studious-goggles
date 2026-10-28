using CommunityToolkit.Maui;
using ImageGen.Core;
using ImageGen.Core.Platform;
using ImageGen.Data;
using ImageGen.Maui.Pages;
using ImageGen.Maui.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
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
        builder.Services.AddSingleton<IMainThread, MauiMainThread>();

        // Pages and page models
        builder.Services.AddSingleton<SessionsPageModel>();
        builder.Services.AddSingleton<SessionsPage>();
        builder.Services.AddTransientWithShellRoute<ApiKeyPage, ApiKeyPageModel>(Routes.ApiKey);
        builder.Services.AddTransientWithShellRoute<ChatPage, ChatPageModel>(Routes.Chat);
        builder.Services.AddTransientWithShellRoute<SessionModelsPage, SessionModelsPageModel>(Routes.SessionModels);

        return builder.Build();
    }
}
