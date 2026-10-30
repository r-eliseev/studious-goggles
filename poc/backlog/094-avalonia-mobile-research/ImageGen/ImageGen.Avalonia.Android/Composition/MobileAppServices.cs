using Avalonia.Controls;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Avalonia.Platform;
using ImageGen.Presentation.Appearance;
using ImageGen.Core;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Data;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Composition;

public static class MobileAppServices
{
    public static ServiceProvider Create(IAppPaths paths, ISecretStore secrets, ISecretPersistence persistence)
    {
        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(secrets);
        services.AddSingleton(persistence);
        services.AddLogging();
        services.AddImageGenCore();
        services.AddImageGenData();
        services.AddSingleton<AppInitializer>();
        services.AddSingleton<IMainThread, AvaloniaMainThread>();
        services.AddSingleton<IDialogService, OverlayDialogService>();
        services.AddSingleton<IFilePickerService, TopLevelFilePickerService>();
        services.AddSingleton<IClipboardService, TopLevelClipboardService>();
        services.AddSingleton<IImageSaver, AndroidImageSaver>();
        services.AddSingleton<IFolderLauncher, MobileFolderLauncher>();
        services.AddSingleton<AppearanceService>();
        services.AddSingleton<ICurrentSessionTracker, CurrentSessionTracker>();
        services.AddSingleton(sp => new PageRoutes()
            .Add(Routes.Sessions, _ => new SessionsView { DataContext = sp.GetRequiredService<SessionsPageModel>() })
            .Add(Routes.ApiKey, _ => new ApiKeyView { DataContext = sp.GetRequiredService<ApiKeyPageModel>() })
            .Add(Routes.Chat, query => CreatePage(sp.GetRequiredService<ChatPageModel>(), query, model => new ChatView { DataContext = model, SessionPicker = sp.GetRequiredService<SessionsPageModel>() }))
            .Add(Routes.SessionModels, query => CreatePage(sp.GetRequiredService<SessionModelsPageModel>(), query, model => new SessionModelsView { DataContext = model }))
            .Add(Routes.SessionParameters, query => CreatePage(sp.GetRequiredService<SessionParametersPageModel>(), query, model => new SessionParametersView { DataContext = model }))
            .Add(Routes.ModelPicker, query => CreatePage(sp.GetRequiredService<ModelPickerPageModel>(), query, model => new ModelPickerView { DataContext = model }))
            .Add(Routes.Settings, _ => new SettingsView { DataContext = sp.GetRequiredService<SettingsPageModel>() }));
        services.AddSingleton<AvaloniaNavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<AvaloniaNavigationService>());
        services.AddSingleton<SessionsPageModel>();
        services.AddTransient<ApiKeyPageModel>();
        services.AddTransient<ChatPageModel>();
        services.AddTransient<SettingsPageModel>();
        services.AddTransient<SessionModelsPageModel>();
        services.AddTransient<SessionParametersPageModel>();
        services.AddTransient<ModelPickerPageModel>();
        services.AddSingleton<Func<SessionModelsPageModel>>(sp => sp.GetRequiredService<SessionModelsPageModel>);
        services.AddSingleton<Func<SessionParametersPageModel>>(sp => sp.GetRequiredService<SessionParametersPageModel>);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static Control CreatePage<T>(T model, IReadOnlyDictionary<string, string> query, Func<T, Control> make) where T : class
    {
        var values = query.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal);
        switch (model)
        {
            case ChatPageModel chat: chat.ApplyQueryAttributes(values); break;
            case SessionModelsPageModel models: models.ApplyQueryAttributes(values); break;
            case SessionParametersPageModel parameters: parameters.ApplyQueryAttributes(values); break;
            case ModelPickerPageModel picker: picker.ApplyQueryAttributes(values); break;
        }
        return make(model);
    }
}
