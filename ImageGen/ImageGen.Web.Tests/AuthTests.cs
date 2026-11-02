using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImageGen.Web.Tests;

[Collection(WebTestCollection.Name)]
public sealed class AuthTests
{
    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorizedWithoutCookie()
    {
        using var host = new TestHost("correct-password");
        using var client = host.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { password = "wrong-password" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutLogin_ReturnsJsonUnauthorized()
    {
        using var host = new TestHost("correct-password");
        using var client = host.Factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType!.MediaType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_WithoutAntiforgeryHeader_IsRejected()
    {
        using var host = new TestHost("correct-password");
        using var client = host.Factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { password = "correct-password" }, TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();

        var response = await client.PostAsync("/api/auth/logout", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf", TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(csrf?.Token));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        var logout = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
    }

    [Fact]
    public async Task Login_CookieIsSecureOutsideLocalhost()
    {
        using var host = new TestHost("correct-password");
        using var client = host.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { password = "correct-password" }),
        };
        request.Headers.Host = "imagegen.example";

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("; secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_OverRateLimit_ReturnsTooManyRequests()
    {
        using var host = new TestHost("correct-password");
        using var client = host.Factory.CreateClient();

        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            response = await client.PostAsJsonAsync("/api/auth/login", new { password = "wrong-password" }, TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
    }

    private sealed class TestHost : IDisposable
    {
        private readonly string? _oldDataDirectory = Environment.GetEnvironmentVariable("IMAGEGEN_DATA_DIR");
        private readonly string? _oldPassword = Environment.GetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD");
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-web-tests", Guid.NewGuid().ToString("N"));

        public TestHost(string password)
        {
            Environment.SetEnvironmentVariable("IMAGEGEN_DATA_DIR", _directory);
            Environment.SetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD", password);
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        }

        public WebApplicationFactory<Program> Factory { get; }

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
