using ImageGen.Core.OpenRouter;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core services and the OpenRouter provider.
    /// The host must register <c>ISecretStore</c> and <c>IAppPaths</c> itself and call <c>AddImageGenData()</c>
    /// (from ImageGen.Data) for the storage services; resolving the core services fails without them.
    /// </summary>
    public static IServiceCollection AddImageGenCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        // One typed client for OpenRouter. The interfaces below hand out the same type.
        services.AddHttpClient<OpenRouterClient>(client =>
        {
            client.BaseAddress = OpenRouterClient.BaseAddress;
            // Some image models take longer than the default 100 seconds.
            client.Timeout = TimeSpan.FromMinutes(3);
        });
        services.AddTransient<IModelProvider>(sp => sp.GetRequiredService<OpenRouterClient>());
        services.AddTransient<IImageGenerator>(sp => sp.GetRequiredService<OpenRouterClient>());
        services.AddTransient<IOpenRouterChatClient>(sp => sp.GetRequiredService<OpenRouterClient>());

        services.AddSingleton<ISeedSource, RandomSeedSource>();
        services.AddSingleton<ApiKeyService>();
        services.AddSingleton<ModelCatalogService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<GenerationService>();
        services.AddSingleton<AssistantService>();

        return services;
    }
}
