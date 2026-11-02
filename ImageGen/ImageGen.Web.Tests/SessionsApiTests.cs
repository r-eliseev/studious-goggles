using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImageGen.Web.Tests;

[Collection(WebTestCollection.Name)]
public sealed class SessionsApiTests
{
    [Fact]
    public async Task Sessions_WithoutAuthentication_ReturnsJsonUnauthorized()
    {
        using var host = new TestHost();
        using var client = host.Factory.CreateClient();

        var response = await client.GetAsync("/api/sessions", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Sessions_CreateAndReadTimeline_UsesCoreService()
    {
        using var host = new TestHost();
        using var client = host.Factory.CreateClient();
        await host.LoginAsync(client);
        var token = await host.GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sessions");
        request.Headers.Add("X-CSRF-TOKEN", token);

        var create = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var session = await create.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(session);

        var timeline = await client.GetFromJsonAsync<TimelineResponse>($"/api/sessions/{session.Id}/timeline", TestContext.Current.CancellationToken);
        Assert.NotNull(timeline);
        Assert.Equal(session.Id, timeline.Session.Id);
        Assert.Empty(timeline.Generations);
    }

    [Fact]
    public async Task Providers_ListDoesNotExposeProviderConfigurationOrSecrets()
    {
        using var host = new TestHost();
        using var client = host.Factory.CreateClient();
        await host.LoginAsync(client);

        var response = await client.GetStringAsync("/api/providers", TestContext.Current.CancellationToken);

        Assert.Contains("openrouter", response, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("baseAddress", response, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SessionResponse(Guid Id, string Title);

    private sealed record TimelineResponse(SessionResponse Session, IReadOnlyList<object> Messages, IReadOnlyList<object> Runs, IReadOnlyList<object> Generations);

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
