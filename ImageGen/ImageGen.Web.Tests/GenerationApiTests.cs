using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImageGen.Web.Tests;

[Collection(WebTestCollection.Name)]
public sealed class GenerationApiTests
{
    [Fact]
    public async Task AssistantMessage_UsesCoreAndPersistsWithoutProviderKey()
    {
        using var host = new TestHost();
        using var client = host.Factory.CreateClient();
        await host.LoginAsync(client);
        var token = await host.GetCsrfTokenAsync(client);
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/sessions");
        create.Headers.Add("X-CSRF-TOKEN", token);
        var sessionResponse = await client.SendAsync(create, TestContext.Current.CancellationToken);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("hello"), "text");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/sessions/{session!.Id:D}/messages") { Content = form };
        message.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MessageResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("User", result.User.Role);
        Assert.Equal("Assistant", result.Assistant.Role);
    }

    [Fact]
    public async Task GenerationImage_UnknownId_ReturnsNotFound()
    {
        using var host = new TestHost();
        using var client = host.Factory.CreateClient();
        await host.LoginAsync(client);

        var response = await client.GetAsync($"/api/generations/{Guid.NewGuid():D}/image", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerationRepeat_WithoutCsrfToken_IsRejected()
    {
        using var host = new TestHost();
        using var client = host.Factory.CreateClient();
        await host.LoginAsync(client);

        var response = await client.PostAsync($"/api/generations/{Guid.NewGuid():D}/repeat", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerationRepeat_UnknownId_ReturnsNotFound()
    {
        using var host = new TestHost();
        using var client = host.Factory.CreateClient();
        await host.LoginAsync(client);
        var token = await host.GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/generations/{Guid.NewGuid():D}/repeat");
        request.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record SessionResponse(Guid Id);

    private sealed record MessageResponse(ChatResponse User, ChatResponse Assistant, Guid? RunId);

    private sealed record ChatResponse(string Role, string Text, bool IsError);

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
