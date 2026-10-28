using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using ImageGen.Avalonia.Platform;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Core;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Data;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Composition;

public static class AppServices
{
    /// <summary>
    /// Builds the container of the desktop app: core, storage, the shared presentation layer, the Avalonia pages and the
    /// platform services. Validates the registrations at build time.
    /// <paramref name="openRouterHandler"/> and <paramref name="dialogs"/> replace the network and the dialog windows
    /// (tests); the app itself passes nothing.
    /// </summary>
    public static ServiceProvider Create(
        IAppPaths paths,
        ISecretStore secrets,
        ISecretPersistence persistence,
        HttpMessageHandler? openRouterHandler = null,
        IDialogService? dialogs = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(persistence);

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(secrets);
        services.AddSingleton(persistence);
        services.AddImageGenCore();
        services.AddImageGenData();
        if (openRouterHandler is not null)
        {
            services.AddHttpClient<OpenRouterClient>().ConfigurePrimaryHttpMessageHandler(() => openRouterHandler);
        }

        services.AddSingleton<AppInitializer>();
        services.AddSingleton<IMainThread, AvaloniaMainThread>();
        services.AddSingleton<IDialogService>(dialogs ?? new AvaloniaDialogService(MainWindowOwner));

        services.AddSingleton(sp => new PageRoutes()
            .Add(Routes.Sessions, _ => new SessionsView { DataContext = sp.GetRequiredService<SessionsPageModel>() })
            .Add(Routes.ApiKey, _ => new ApiKeyView { DataContext = sp.GetRequiredService<ApiKeyPageModel>() })
            .Add(Routes.Chat, _ => new ComingSoonView())
            .Add(Routes.SessionModels, _ => new ComingSoonView()));
        services.AddSingleton<AvaloniaNavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<AvaloniaNavigationService>());

        services.AddSingleton<SessionsPageModel>();
        services.AddTransient<ApiKeyPageModel>();
        services.AddTransient<ChatPageModel>();
        services.AddTransient<SessionModelsPageModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private static Window? MainWindowOwner() =>
        Application.Current is App app ? app.MainWindow : null;
}
