using ImageGen.Core.AnthropicCompatible;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Fal;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public class ProviderRouterTests
{
    private const string Json = """
        {"providers":[
          {"id":"openrouter","kind":"openRouter","displayName":"OpenRouter","baseAddress":"https://openrouter.ai/api/v1/","timeoutSeconds":180},
          {"id":"local","kind":"openAiCompatible","displayName":"Local","baseAddress":"http://localhost:1234/v1/","timeoutSeconds":60},
          {"id":"local-two","kind":"openAiCompatible","displayName":"Local Two","baseAddress":"http://localhost:5678/v1/","timeoutSeconds":60},
          {"id":"fal","kind":"fal","displayName":"fal.ai","baseAddress":"https://queue.fal.run/","timeoutSeconds":60},
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
        Assert.Equal(["openrouter", "local", "local-two", "fal"], _router.ImageProviderIds);
    }

    [Fact]
    public void GetConnection_KindOpenRouter_ReturnsInMemoryWrapperWithQueuedCapability()
    {
        var connection = Assert.IsType<InMemoryQueuedImageConnection>(_router.GetConnection("openrouter"));

        Assert.Same(connection, _router.GetImageProvider("openrouter"));
        Assert.Same(connection, _router.GetChatClient("openrouter"));
        Assert.Same(connection, _router.GetQueuedImageProvider("openrouter"));
    }

    [Fact]
    public void GetConnection_KindOpenAiCompatible_ReturnsInMemoryWrapperWithQueuedCapability()
    {
        var connection = Assert.IsType<InMemoryQueuedImageConnection>(_router.GetConnection("local"));

        Assert.Same(connection, _router.GetImageProvider("local"));
        Assert.Same(connection, _router.GetChatClient("local"));
        Assert.Same(connection, _router.GetQueuedImageProvider("local"));
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
    public void GetConnection_Fal_RemainsNativeQueuedProvider()
    {
        var connection = _router.GetConnection("fal");

        Assert.IsType<FalImageProvider>(connection);
        Assert.IsAssignableFrom<IQueuedImageProvider>(connection);
        Assert.False(connection is InMemoryQueuedImageConnection);
    }

    [Fact]
    public void GetChatClient_OpenRouter_ReturnsSameInstanceAsImageProvider()
    {
        Assert.Same(_router.GetImageProvider("openrouter"), _router.GetChatClient("openrouter"));
    }

    [Fact]
    public void GetConnection_UnknownId_ThrowsModelUnavailableWithoutCreatingClient()
    {
        var ex = Assert.Throws<ProviderException>(() => _router.GetConnection("missing"));

        Assert.Equal(ErrorKind.ModelUnavailable, ex.Kind);
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void GetConnection_SameId_ReturnsSameClient()
    {
        Assert.Same(_router.GetConnection("local"), _router.GetConnection("local"));
    }

    [Fact]
    public void GetConnection_ConcurrentSameId_ReturnsOneCachedConnection()
    {
        var connections = new IProviderConnection[32];
        Parallel.For(0, connections.Length, index => connections[index] = _router.GetConnection("local"));

        Assert.All(connections, connection => Assert.Same(connections[0], connection));
    }

    [Fact]
    public async Task GetQueuedImageProvider_CompatibleProviders_HaveIsolatedAdapters()
    {
        var first = _router.GetQueuedImageProvider("local");
        var second = _router.GetQueuedImageProvider("local-two");
        var requestId = Guid.NewGuid().ToString("N");
        var handle = new ProviderJobHandle(
            "local",
            requestId,
            $"memory://local/{requestId}/status",
            $"memory://local/{requestId}/result",
            $"memory://local/{requestId}/cancel");

        var interrupted = await first.PollAsync(handle, TestContext.Current.CancellationToken);
        var exception = await Assert.ThrowsAsync<ProviderException>(() => second.PollAsync(handle, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Interrupted, interrupted.ErrorKind);
        Assert.Equal(ErrorKind.Unknown, exception.Kind);
    }
}
