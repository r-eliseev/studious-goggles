using ImageGen.Avalonia.Platform;
using ImageGen.Avalonia.Services;
using ImageGen.Core;
using ImageGen.Core.Platform;
using ImageGen.Data;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Composition;

public static class AppServices
{
    /// <summary>
    /// Builds the container of the desktop app: core, storage, the shared presentation layer and the Avalonia
    /// implementations of the platform services. Validates the registrations at build time.
    /// </summary>
    public static ServiceProvider Create(IAppPaths paths, ISecretStore secrets)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(secrets);

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(secrets);
        services.AddImageGenCore();
        services.AddImageGenData();

        services.AddSingleton<AppInitializer>();
        services.AddSingleton<IMainThread, AvaloniaMainThread>();
        services.AddSingleton<IDialogService, DialogsNotAvailable>();
        services.AddSingleton<INavigationService, NavigationNotAvailable>();

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
}
