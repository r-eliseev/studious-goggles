using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public sealed class E2eProvidersConfigTests
{
    [Fact]
    public void Load_ConfigFile_RegistersOnlyTheExpectedMockModels()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "e2e-providers.json");
        Assert.True(File.Exists(path), $"Expected E2E provider config at {path}.");

        var json = File.ReadAllText(path);
        var registry = ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), json);
        var provider = registry.Get("e2e-mock");

        Assert.Equal("https://localhost:5443/v1/", provider.BaseAddress.AbsoluteUri);
        Assert.Equal(["e2e/image"], provider.ImageModels.Select(model => model.Id));
        Assert.Equal(["e2e/assistant"], provider.ChatModels!.Select(model => model.Id));
    }
}
