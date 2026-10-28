using ImageGen.Avalonia.Platform;
using ImageGen.Avalonia.Services.Secrets;

namespace ImageGen.Avalonia.Tests.Services.Secrets;

public class DesktopSecretStoreTests
{
    private readonly ISecretBackend _backend = Substitute.For<ISecretBackend>();

    [Fact]
    public async Task IsPersistentAsync_NoPlatformBackend_ReturnsFalse()
    {
        var store = new DesktopSecretStore(null);

        Assert.False(await store.IsPersistentAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsPersistentAsync_BackendUnavailable_ReturnsFalse()
    {
        _backend.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);
        var store = new DesktopSecretStore(_backend);

        Assert.False(await store.IsPersistentAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsPersistentAsync_BackendAvailable_ReturnsTrue()
    {
        _backend.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        var store = new DesktopSecretStore(_backend);

        Assert.True(await store.IsPersistentAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetAsync_BackendUnavailable_KeepsValueInMemoryOnly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _backend.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);
        var store = new DesktopSecretStore(_backend);

        await store.SetAsync("openrouter_api_key", "sk-or-test", cancellationToken);

        Assert.Equal("sk-or-test", await store.GetAsync("openrouter_api_key", cancellationToken));
        await _backend.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_BackendAvailable_WritesThroughBackend()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _backend.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        _backend.GetAsync("openrouter_api_key", Arg.Any<CancellationToken>()).Returns("sk-or-stored");
        var store = new DesktopSecretStore(_backend);

        await store.SetAsync("openrouter_api_key", "sk-or-test", cancellationToken);

        await _backend.Received(1).SetAsync("openrouter_api_key", "sk-or-test", Arg.Any<CancellationToken>());
        Assert.Equal("sk-or-stored", await store.GetAsync("openrouter_api_key", cancellationToken));
    }

    [Fact]
    public async Task RemoveAsync_BackendAvailable_RemovesThroughBackend()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _backend.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        var store = new DesktopSecretStore(_backend);

        await store.RemoveAsync("openrouter_api_key", cancellationToken);

        await _backend.Received(1).RemoveAsync("openrouter_api_key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_BackendThrows_ReturnsNullSoTheKeyIsAskedAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _backend.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        _backend.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<string?>(new InvalidOperationException("locked")));
        var store = new DesktopSecretStore(_backend);

        Assert.Null(await store.GetAsync("openrouter_api_key", cancellationToken));
    }

    [Fact]
    public async Task IsPersistentAsync_ProbeFails_ReturnsFalseAndDoesNotThrow()
    {
        _backend.IsAvailableAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<bool>(new InvalidOperationException("no bus")));
        var store = new DesktopSecretStore(_backend);

        Assert.False(await store.IsPersistentAsync(TestContext.Current.CancellationToken));
    }
}
