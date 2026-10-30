using ImageGen.Core.Context;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Fal;
using ImageGen.Core.Http;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImageGen.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core services and the providers. Providers come from the built-in config and the optional
    /// user file <c>providers.json</c> in <c>IAppPaths.DataDirectory</c>; an invalid config throws
    /// <see cref="ProviderConfigException"/> when the registry is first resolved (at app start).
    /// The host must register <c>ISecretStore</c> and <c>IAppPaths</c> itself and call <c>AddImageGenData()</c>
    /// (from ImageGen.Data) for the storage services; resolving the core services fails without them.
    /// </summary>
    public static IServiceCollection AddImageGenCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        // The shell registers the file trace before this call when debug tracing is on (task 085); the default records nothing.
        services.TryAddSingleton<IDebugTrace>(NullDebugTrace.Instance);

        // Clients take HttpClient from IHttpClientFactory per request. The timeout is set per provider
        // in the request itself, so the default HttpClient timeout must not cut it short.
        services.AddHttpClient();
        services.AddHttpClient(FalImageProvider.TransportName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddTransient<LoggingHttpHandler>();
        services.ConfigureHttpClientDefaults(builder => builder
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .AddHttpMessageHandler<LoggingHttpHandler>());

        services.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<IAppPaths>();
            return ProviderConfigLoader.Build(
                ProviderConfigLoader.ReadBuiltInJson(),
                ProviderConfigLoader.ReadUserJson(paths.DataDirectory));
        });
        services.AddSingleton<IProviderRouter, ProviderRouter>();

        services.AddSingleton<ISeedSource, RandomSeedSource>();
        services.AddSingleton<ApiKeyService>();
        services.AddSingleton<ModelCatalogService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<SessionChangeNotifier>();
        services.AddSingleton<AttachmentService>();
        services.AddSingleton<ImageDescriptionService>();
        services.AddSingleton<GenerationService>();
        services.AddSingleton<ISessionGenerationCoordinator>(sp => sp.GetRequiredService<GenerationService>());
        services.AddSingleton<ChatModelCatalogService>();
        services.AddSingleton<ConversationSummaryService>();
        services.AddSingleton<AssistantService>();
        services.AddSingleton<TokenCorrection>();
        services.AddSingleton<SessionSearchService>();

        return services;
    }
}
