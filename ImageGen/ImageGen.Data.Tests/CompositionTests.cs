using ImageGen.Core;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageGen.Data.Tests;

public class CompositionTests
{
    [Fact]
    public void AddImageGenCoreAndData_BuildWithValidation_ResolvesAllServices()
    {
        using var provider = BuildProvider();

        Assert.IsType<OpenRouterClient>(provider.GetRequiredService<IModelProvider>());
        Assert.IsType<OpenRouterClient>(provider.GetRequiredService<IImageGenerator>());
        Assert.IsType<OpenRouterClient>(provider.GetRequiredService<IOpenRouterChatClient>());
        Assert.NotNull(provider.GetRequiredService<ISeedSource>());
        Assert.NotNull(provider.GetRequiredService<ApiKeyService>());
        Assert.NotNull(provider.GetRequiredService<ModelCatalogService>());
        Assert.NotNull(provider.GetRequiredService<SessionService>());
        Assert.NotNull(provider.GetRequiredService<GenerationService>());
        Assert.NotNull(provider.GetRequiredService<AssistantService>());
    }

    [Fact]
    public void AddImageGenCore_ServicesAreSingletons()
    {
        using var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<GenerationService>(), provider.GetRequiredService<GenerationService>());
        Assert.Same(provider.GetRequiredService<AssistantService>(), provider.GetRequiredService<AssistantService>());
        Assert.Same(provider.GetRequiredService<SessionService>(), provider.GetRequiredService<SessionService>());
    }

    private static ServiceProvider BuildProvider()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(Path.GetTempPath());
        var secrets = Substitute.For<ISecretStore>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(paths);
        services.AddSingleton(secrets);
        services.AddImageGenCore();
        services.AddImageGenData();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
