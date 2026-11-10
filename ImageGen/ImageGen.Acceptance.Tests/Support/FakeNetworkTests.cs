using System.Net;
using System.Net.Http;
using System.Text;
using ImageGen.Acceptance.Tests.Support;

namespace ImageGen.Acceptance.Tests.Support;

public sealed class FakeNetworkTests
{
    [Fact]
    public async Task SendAsync_UnknownAddressFailsAndDoesNotLogKey()
    {
        using var network = new FakeNetwork();
        network.RegisterSecret("sk-test-secret");
        using var client = new HttpClient(network, disposeHandler: false);
        using var content = new StringContent("""{"api_key":"sk-test-secret"}""", Encoding.UTF8, "application/json");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.PostAsync("https://example.test/unexpected?api_key=sk-test-secret", content, TestContext.Current.CancellationToken));

        Assert.Contains("POST https://example.test/unexpected", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-secret", exception.Message, StringComparison.Ordinal);
        var request = Assert.Single(network.Requests);
        Assert.DoesNotContain("sk-test-secret", request.Address, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-secret", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_MatchingRouteRecordsSafeRequestAndReturnsConfiguredResponse()
    {
        using var network = new FakeNetwork();
        network.AddResponse(HttpMethod.Post, "example.test", "/images", HttpStatusCode.OK, "{\"ok\":true}");
        using var client = new HttpClient(network, disposeHandler: false);
        using var content = new StringContent("{\"model\":\"vendor/model\",\"seed\":9}", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("https://example.test/images", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("POST", Assert.Single(network.Requests).Method);
        Assert.Contains("\"seed\":9", network.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_HeldResponseCanBeReleasedOrCancelled()
    {
        using var network = new FakeNetwork();
        var held = network.AddHeldResponse(HttpMethod.Get, "example.test", "/slow");
        using var client = new HttpClient(network, disposeHandler: false);
        using var cancellation = new CancellationTokenSource();
        var pending = client.GetAsync("https://example.test/slow", cancellation.Token);

        await held.WaitUntilRequestedAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        held.Release(HttpStatusCode.OK, "released");
    }
}
