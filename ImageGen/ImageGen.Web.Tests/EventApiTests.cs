using System.Net;
using System.Net.Http.Json;
using ImageGen.Web.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    [Fact]
    public async Task FullSubscriberQueue_SendsResyncWithoutFurtherEvents()
    {
        using var host = new TestHost();
        var hub = host.Factory.Services.GetRequiredService<EventHub>();
        using var subscription = hub.Subscribe();
        var lastSessionId = Guid.Empty;

        for (var index = 0; index < 257; index++)
        {
            lastSessionId = Guid.NewGuid();
            host.NotifySessionChange(lastSessionId);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
        var marker = await subscription.Reader.ReadAsync(timeout.Token);
        Assert.Equal("resync", marker.Name);
        Assert.Equal("{}", marker.Data);
        var latest = await subscription.Reader.ReadAsync(timeout.Token);
        Assert.Equal("session", latest.Name);
        Assert.Equal($"{{\"sessionId\":\"{lastSessionId:D}\"}}", latest.Data);
        Assert.False(subscription.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Events_AfterIdleHeartbeat_StillDeliversSessionEvent()
    {
        using var host = new TestHost(TimeSpan.FromMilliseconds(100));
        using var client = host.Factory.CreateClient();
        await host.LoginAsync(client);
        using var response = await host.OpenEventsAsync(client);
        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        await Task.Delay(TimeSpan.FromMilliseconds(350), TestContext.Current.CancellationToken);

        var sessionId = Guid.NewGuid();
        host.NotifySessionChange(sessionId);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var heartbeatCount = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(timeout.Token)) is not null && line != "event: session")
        {
            if (line == ": heartbeat") heartbeatCount++;
        }

        Assert.Equal("event: session", line);
        Assert.InRange(heartbeatCount, 3, 10);
        Assert.Equal($"data: {{\"sessionId\":\"{sessionId:D}\"}}", await reader.ReadLineAsync(timeout.Token));
    }

    private sealed class TestHost : IDisposable
    {
        private readonly string? _oldDataDirectory = Environment.GetEnvironmentVariable("IMAGEGEN_DATA_DIR");
        private readonly string? _oldPassword = Environment.GetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD");
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-web-tests", Guid.NewGuid().ToString("N"));

        public TestHost(TimeSpan? heartbeatInterval = null)
        {
            Environment.SetEnvironmentVariable("IMAGEGEN_DATA_DIR", _directory);
            Environment.SetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD", "test-password");
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                if (heartbeatInterval is { } interval)
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<EventStreamOptions>();
                        services.AddSingleton(new EventStreamOptions { HeartbeatInterval = interval });
                    });
                }
            });
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
