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
        using var provider = BuildProvider(NewDirectory());
        var router = provider.GetRequiredService<IProviderRouter>();

        Assert.IsType<OpenRouterClient>(router.GetImageProvider(ProviderIds.OpenRouter));
        Assert.IsType<OpenRouterClient>(router.GetChatClient(ProviderIds.OpenRouter));
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
        using var provider = BuildProvider(NewDirectory());

        Assert.Same(provider.GetRequiredService<GenerationService>(), provider.GetRequiredService<GenerationService>());
        Assert.Same(provider.GetRequiredService<AssistantService>(), provider.GetRequiredService<AssistantService>());
        Assert.Same(provider.GetRequiredService<SessionService>(), provider.GetRequiredService<SessionService>());
    }

    [Fact]
    public void UserConfigFile_AddsProviderToRegistry()
    {
        var directory = NewDirectory();
        File.WriteAllText(
            Path.Combine(directory, ProviderConfigLoader.UserFileName),
            """{"providers":[{"id":"local","kind":"openAiCompatible","displayName":"Local","baseAddress":"http://localhost:1234/v1","timeoutSeconds":60}]}""");
        using var provider = BuildProvider(directory);

        var registry = provider.GetRequiredService<ProviderRegistry>();

        Assert.Equal(["openrouter", "fal", "local"], registry.All.Select(p => p.Id));
    }

    [Fact]
    public void InvalidUserConfigFile_ThrowsWhenRegistryIsResolved()
    {
        var directory = NewDirectory();
        File.WriteAllText(Path.Combine(directory, ProviderConfigLoader.UserFileName), """{"providers":[{"id":"x","kind":"carrier"}]}""");
        using var provider = BuildProvider(directory);

        var ex = Assert.Throws<ProviderConfigException>(() => provider.GetRequiredService<ProviderRegistry>());

        Assert.Contains("carrier", ex.Message);
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static ServiceProvider BuildProvider(string dataDirectory)
    {
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(dataDirectory);
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
