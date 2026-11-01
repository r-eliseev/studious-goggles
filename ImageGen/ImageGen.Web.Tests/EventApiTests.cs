using System.Net;
using System.Net.Http.Json;
using ImageGen.Web.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Web.Tests;

[Collection(WebTestCollection.Name)]
public sealed class EventApiTests
{
    [Fact]
    public async Task Events_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var host = new TestHost();
        using var client = host.Factory.CreateClient();

        var response = await client.GetAsync("/api/events", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SessionChange_IsSentToAllEventSubscribers()
    {
        using var host = new TestHost();
        using var first = host.Factory.CreateClient();
        using var second = host.Factory.CreateClient();
        await host.LoginAsync(first);
        await host.LoginAsync(second);
        var hub = host.Factory.Services.GetRequiredService<EventHub>();
        using var firstSubscription = hub.Subscribe();
        using var secondSubscription = hub.Subscribe();
        var sessionId = Guid.NewGuid();

        host.NotifySessionChange(sessionId);

        var firstEvent = await firstSubscription.Reader.ReadAsync(TestContext.Current.CancellationToken);
        var secondEvent = await secondSubscription.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("session", firstEvent.Name);
        Assert.Equal($"{{\"sessionId\":\"{sessionId:D}\"}}", firstEvent.Data);
        Assert.Equal(firstEvent, secondEvent);
    }

    [Fact]
    public async Task FullSubscriberQueue_IsReplacedWithResyncMarker()
    {
        using var host = new TestHost();
        var hub = host.Factory.Services.GetRequiredService<EventHub>();
        using var subscription = hub.Subscribe();

        for (var index = 0; index < 258; index++)
        {
            host.NotifySessionChange(Guid.NewGuid());
        }

        var marker = await subscription.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("resync", marker.Name);
        Assert.Equal("{}", marker.Data);
        var next = await subscription.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("session", next.Name);
    }

    private sealed class TestHost : IDisposable
    {
        private readonly string? _oldDataDirectory = Environment.GetEnvironmentVariable("IMAGEGEN_DATA_DIR");
        private readonly string? _oldPassword = Environment.GetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD");
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-web-tests", Guid.NewGuid().ToString("N"));

        public TestHost()
        {
            Environment.SetEnvironmentVariable("IMAGEGEN_DATA_DIR", _directory);
            Environment.SetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD", "test-password");
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        }

        public WebApplicationFactory<Program> Factory { get; }

        public async Task LoginAsync(HttpClient client)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { password = "test-password" }, TestContext.Current.CancellationToken);
            login.EnsureSuccessStatusCode();
        }

        public async Task<string> GetCsrfTokenAsync(HttpClient client)
        {
            var token = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf", TestContext.Current.CancellationToken);
            return token!.Token;
        }

        public async Task<HttpResponseMessage> OpenEventsAsync(HttpClient client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/events");
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        }

        public void NotifySessionChange(Guid sessionId) =>
            Factory.Services.GetRequiredService<ImageGen.Core.Services.SessionChangeNotifier>().Notify(sessionId);

        public void Dispose()
        {
            Factory.Dispose();
            Environment.SetEnvironmentVariable("IMAGEGEN_DATA_DIR", _oldDataDirectory);
            Environment.SetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD", _oldPassword);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }

    private sealed record CsrfResponse(string Token);
}
