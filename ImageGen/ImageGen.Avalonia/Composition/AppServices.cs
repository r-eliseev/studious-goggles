using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using ImageGen.Avalonia.Platform;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Core;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using ImageGen.Data;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Search;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Settings;
using ImageGen.Presentation.Spending;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Composition;

public static partial class AppServices
{
    /// <summary>Builds the shared container and validates registrations at build time.</summary>
    /// <param name="registerPlatform">Adds the platform-specific services required by the shared pages.</param>
    /// <param name="openRouterHandler">Replaces the OpenRouter network handler in tests.</param>
    /// <param name="configureLogging">Adds log providers; the app adds the file log.</param>
    public static ServiceProvider CreateShared(
        IAppPaths paths,
        ISecretStore secrets,
        ISecretPersistence persistence,
        Action<IServiceCollection> registerPlatform,
        HttpMessageHandler? openRouterHandler = null,
        Action<ILoggingBuilder>? configureLogging = null,
        IDebugTrace? debugTrace = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(registerPlatform);

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(secrets);
        services.AddSingleton(persistence);
        services.AddLogging(builder => configureLogging?.Invoke(builder));
        if (debugTrace is not null)
        {
            services.AddSingleton(debugTrace);
        }

        services.AddImageGenCore();
        services.AddImageGenData();
        if (openRouterHandler is not null)
        {
            services.AddHttpClient(ProviderIds.OpenRouter).ConfigurePrimaryHttpMessageHandler(() => openRouterHandler);
        }

        services.AddSingleton<AppInitializer>();
        services.AddSingleton<IMainThread, AvaloniaMainThread>();
        services.AddSingleton<AppearanceService>();
        services.AddSingleton<SpendingSettingsFile>();
        services.AddSingleton<SpendingService>();
        services.AddSingleton<SearchSettingsFile>();
        services.AddSingleton<ImageGen.Core.Search.ISemanticSearchSettingsStore>(sp => sp.GetRequiredService<SearchSettingsFile>());
        services.AddSingleton<SemanticIndexService>();
        services.AddSingleton<SemanticSearchService>();
        services.AddSingleton<ISemanticIndexStatusProvider>(sp => sp.GetRequiredService<SemanticIndexService>());
        services.AddSingleton<ISemanticIndexController>(sp => sp.GetRequiredService<SemanticIndexService>());
        services.AddSingleton<ICurrentSessionTracker, CurrentSessionTracker>();
        services.AddSingleton<ErrorAlert>();
        services.AddSingleton<DefaultAssistantModelFile>();
        services.AddSingleton<IDefaultAssistantModel>(sp => sp.GetRequiredService<DefaultAssistantModelFile>());

        services.AddSingleton(sp => new PageRoutes()
            .Add(Routes.Sessions, _ => new SessionsView { DataContext = sp.GetRequiredService<SessionsPageModel>() })
            .Add(Routes.ApiKey, query => Page(sp.GetRequiredService<ApiKeyPageModel>(), query, model => new ApiKeyView { DataContext = model }))
            .Add(Routes.Chat, query => Page(sp.GetRequiredService<ChatPageModel>(), query, model => new ChatView { DataContext = model, SessionPicker = sp.GetRequiredService<SessionsPageModel>() }))
            .Add(Routes.SessionModels, query => Page(sp.GetRequiredService<SessionModelsPageModel>(), query, model => new SessionModelsView { DataContext = model }))
            .Add(Routes.SessionParameters, query => Page(sp.GetRequiredService<SessionParametersPageModel>(), query, model => new SessionParametersView { DataContext = model }))
            .Add(Routes.ModelPicker, query => Page(sp.GetRequiredService<ModelPickerPageModel>(), query, model => new ModelPickerView { DataContext = model }))
            .Add(Routes.Settings, _ => Page(sp.GetRequiredService<SettingsPageModel>(), new Dictionary<string, string>(), model => new SettingsView { DataContext = model }))
            .Add(Routes.Gallery, _ => Page(sp.GetRequiredService<GalleryPageModel>(), new Dictionary<string, string>(), model => new GalleryView { DataContext = model }))
            .Add(Routes.Spending, _ => Page(sp.GetRequiredService<SpendingPageModel>(), new Dictionary<string, string>(), model => new SpendingView { DataContext = model })));
        services.AddSingleton<AvaloniaNavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<AvaloniaNavigationService>());

        services.AddSingleton<SessionsPageModel>();
        services.AddTransient<ApiKeyPageModel>();
        services.AddTransient<ChatPageModel>();
        services.AddTransient<SettingsPageModel>();
        services.AddTransient<SpendingPageModel>();
        services.AddTransient<GalleryPageModel>();
        services.AddTransient<SessionModelsPageModel>();
        services.AddTransient<SessionParametersPageModel>();
        services.AddTransient<ModelPickerPageModel>();
        // The chat side pane (wide window) creates a fresh page model each time it opens.
        services.AddSingleton<Func<SessionModelsPageModel>>(sp => sp.GetRequiredService<SessionModelsPageModel>);
        services.AddSingleton<Func<SessionParametersPageModel>>(sp => sp.GetRequiredService<SessionParametersPageModel>);

        registerPlatform(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    /// <summary>Gives the route query to the page model (the session id) and creates the view for it.</summary>
    private static Control Page<TModel>(TModel model, IReadOnlyDictionary<string, string> query, Func<TModel, Control> createView)
        where TModel : class
    {
        var values = query.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal);
        switch (model)
        {
            case ChatPageModel chat:
                chat.ApplyQueryAttributes(values);
                break;
            case ApiKeyPageModel apiKey:
                apiKey.ApplyQueryAttributes(values);
                break;
            case SessionModelsPageModel models:
                models.ApplyQueryAttributes(values);
                break;
            case SessionParametersPageModel parameters:
                parameters.ApplyQueryAttributes(values);
                break;
            case ModelPickerPageModel picker:
                picker.ApplyQueryAttributes(values);
                break;
        }

        return createView(model);
    }

}
