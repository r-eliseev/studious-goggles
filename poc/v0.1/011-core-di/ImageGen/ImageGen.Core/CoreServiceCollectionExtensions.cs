using ImageGen.Core.OpenRouter;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers core services. The host must also register <c>ISecretStore</c>, <c>IAppPaths</c>
    /// and storage (<c>AddImageGenData</c> from ImageGen.Data).
    /// </summary>
    public static IServiceCollection AddImageGenCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<OpenRouterClient>(client =>
        {
            client.BaseAddress = OpenRouterClient.BaseAddress;
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
