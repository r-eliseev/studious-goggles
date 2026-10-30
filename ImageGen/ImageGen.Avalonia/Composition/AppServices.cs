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
using ImageGen.Data;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Composition;

public static class AppServices
{
    /// <summary>
    /// Builds the container of the desktop app: core, storage, the shared presentation layer, the Avalonia pages and the
    /// platform services. Validates the registrations at build time.
    /// <paramref name="openRouterHandler"/> and <paramref name="dialogs"/> replace the network and the dialog windows
    /// (tests); the app itself passes nothing. <paramref name="configureLogging"/> adds the log providers (the app adds the file log).
    /// </summary>
    public static ServiceProvider Create(
        IAppPaths paths,
        ISecretStore secrets,
        ISecretPersistence persistence,
        HttpMessageHandler? openRouterHandler = null,
        IDialogService? dialogs = null,
        Action<ILoggingBuilder>? configureLogging = null,
        IDebugTrace? debugTrace = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(persistence);

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
        services.AddSingleton<IDialogService>(dialogs ?? new AvaloniaDialogService(MainWindowOwner));
        services.AddSingleton<IFilePickerService>(new AvaloniaFilePickerService(MainWindowOwner));
        services.AddSingleton<AppearanceService>();
        services.AddSingleton<IClipboardService>(new AvaloniaClipboardService(MainWindowOwner));
        services.AddSingleton<IImageSaver>(new AvaloniaImageSaver(MainWindowOwner));
        services.AddSingleton<AvaloniaFullScreenController>();
        services.AddSingleton<IImageViewerService>(sp => new AvaloniaImageViewerService(
            MainWindowOwner,
            sp.GetRequiredService<AvaloniaFullScreenController>(),
            sp.GetRequiredService<IDialogService>()));
        services.AddSingleton<ICurrentSessionTracker, CurrentSessionTracker>();

        services.AddSingleton(sp => new PageRoutes()
            .Add(Routes.Sessions, _ => new SessionsView { DataContext = sp.GetRequiredService<SessionsPageModel>() })
            .Add(Routes.ApiKey, query => Page(sp.GetRequiredService<ApiKeyPageModel>(), query, model => new ApiKeyView { DataContext = model }))
            .Add(Routes.Chat, query => Page(sp.GetRequiredService<ChatPageModel>(), query, model => new ChatView { DataContext = model, SessionPicker = sp.GetRequiredService<SessionsPageModel>() }))
            .Add(Routes.SessionModels, query => Page(sp.GetRequiredService<SessionModelsPageModel>(), query, model => new SessionModelsView { DataContext = model }))
            .Add(Routes.SessionParameters, query => Page(sp.GetRequiredService<SessionParametersPageModel>(), query, model => new SessionParametersView { DataContext = model }))
            .Add(Routes.ModelPicker, query => Page(sp.GetRequiredService<ModelPickerPageModel>(), query, model => new ModelPickerView { DataContext = model }))
            .Add(Routes.Settings, _ => Page(sp.GetRequiredService<SettingsPageModel>(), new Dictionary<string, string>(), model => new SettingsView { DataContext = model })));
        services.AddSingleton<AvaloniaNavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<AvaloniaNavigationService>());

        services.AddSingleton<SessionsPageModel>();
        services.AddTransient<ApiKeyPageModel>();
        services.AddTransient<ChatPageModel>();
        services.AddTransient<SettingsPageModel>();
        services.AddSingleton<IFolderLauncher>(new AvaloniaFolderLauncher(MainWindowOwner));
        services.AddTransient<SessionModelsPageModel>();
        services.AddTransient<SessionParametersPageModel>();
        services.AddTransient<ModelPickerPageModel>();
        // The chat side pane (wide window) creates a fresh page model each time it opens.
        services.AddSingleton<Func<SessionModelsPageModel>>(sp => sp.GetRequiredService<SessionModelsPageModel>);
        services.AddSingleton<Func<SessionParametersPageModel>>(sp => sp.GetRequiredService<SessionParametersPageModel>);

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

    private static Window? MainWindowOwner() =>
        Application.Current is App app ? app.MainWindow : null;
}
