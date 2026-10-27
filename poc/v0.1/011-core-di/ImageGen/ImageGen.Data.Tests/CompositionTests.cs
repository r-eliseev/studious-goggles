using ImageGen.Core;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Data.Tests;

public class CompositionTests
{
    [Fact]
    public void AddImageGenCoreAndData_WithPlatformServices_ResolvesAllServices()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(Path.GetTempPath());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(paths);
        services.AddSingleton(Substitute.For<ISecretStore>());

        services.AddImageGenCore();
        services.AddImageGenData();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<IModelProvider>());
        Assert.NotNull(provider.GetRequiredService<IOpenRouterChatClient>());
        Assert.NotNull(provider.GetRequiredService<IImageGenerator>());
        Assert.NotNull(provider.GetRequiredService<ApiKeyService>());
        Assert.NotNull(provider.GetRequiredService<ModelCatalogService>());
        Assert.NotNull(provider.GetRequiredService<SessionService>());
        Assert.NotNull(provider.GetRequiredService<GenerationService>());
        Assert.NotNull(provider.GetRequiredService<AssistantService>());
        Assert.Same(provider.GetRequiredService<GenerationService>(), provider.GetRequiredService<GenerationService>());
    }
}
