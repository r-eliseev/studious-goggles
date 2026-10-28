using ImageGen.Core.AnthropicCompatible;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.OpenAiCompatible;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public class ProviderRouterTests
{
    private const string Json = """
        {"providers":[
          {"id":"openrouter","kind":"openRouter","displayName":"OpenRouter","baseAddress":"https://openrouter.ai/api/v1/","timeoutSeconds":180},
          {"id":"local","kind":"openAiCompatible","displayName":"Local","baseAddress":"http://localhost:1234/v1/","timeoutSeconds":60},
          {"id":"claude","kind":"anthropicCompatible","displayName":"Claude","baseAddress":"https://api.anthropic.com/v1/","timeoutSeconds":60}
        ]}
        """;

    private readonly ProviderRouter _router;

    public ProviderRouterTests()
    {
        var registry = ProviderConfigLoader.Build(Json, null);
        _router = new ProviderRouter(registry, Substitute.For<IHttpClientFactory>(), Substitute.For<ISecretStore>(), TimeProvider.System);
    }

    [Fact]
    public void ImageProviderIds_ExcludesAnthropicCompatible()
    {
        Assert.Equal(["openrouter", "local"], _router.ImageProviderIds);
    }

    [Fact]
    public void GetImageProvider_KindOpenRouter_ReturnsOpenRouterClient()
    {
        Assert.IsType<OpenRouterClient>(_router.GetImageProvider("openrouter"));
    }

    [Fact]
    public void GetImageProvider_KindOpenAiCompatible_ReturnsOpenAiClient()
    {
        Assert.IsType<OpenAiCompatibleClient>(_router.GetImageProvider("local"));
    }

    [Fact]
    public void GetImageProvider_AnthropicCompatible_ThrowsModelUnavailable()
    {
        var ex = Assert.Throws<ProviderException>(() => _router.GetImageProvider("claude"));

        Assert.Equal(ErrorKind.ModelUnavailable, ex.Kind);
    }

    [Fact]
    public void GetChatClient_AnthropicCompatible_ReturnsAnthropicClient()
    {
        Assert.IsType<AnthropicCompatibleClient>(_router.GetChatClient("claude"));
    }

    [Fact]
    public void GetChatClient_OpenRouter_ReturnsSameInstanceAsImageProvider()
    {
        Assert.Same(_router.GetImageProvider("openrouter"), _router.GetChatClient("openrouter"));
    }

    [Fact]
    public void GetConnection_UnknownId_ThrowsModelUnavailableWithoutCreatingClient()
    {
        var ex = Assert.Throws<ProviderException>(() => _router.GetConnection("fal"));

        Assert.Equal(ErrorKind.ModelUnavailable, ex.Kind);
        Assert.Contains("fal", ex.Message);
    }

    [Fact]
    public void GetConnection_SameId_ReturnsSameClient()
    {
        Assert.Same(_router.GetConnection("local"), _router.GetConnection("local"));
    }
}
