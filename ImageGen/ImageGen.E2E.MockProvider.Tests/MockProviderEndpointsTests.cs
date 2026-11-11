using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImageGen.E2E.MockProvider.Tests;

public sealed class MockProviderEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ApiKey = "e2e-mock-key";

    [Fact]
    public async Task GetModels_WithMockKey_ReturnsConfiguredModels()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        using var response = await client.GetAsync("/v1/models", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ["e2e/assistant", "e2e/image"],
            document.RootElement.GetProperty("data").EnumerateArray()
                .Select(model => model.GetProperty("id").GetString()).ToArray());
    }

    [Fact]
    public async Task GetModels_WithOtherKey_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-key");

        using var response = await client.GetAsync("/v1/models", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChatCompletions_WithUserPrompt_ReturnsImageToolCall()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        using var response = await client.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "e2e/assistant",
            messages = new[] { new { role = "user", content = "a red square" } },
            tools = Array.Empty<object>(),
            tool_choice = "auto",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var call = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("tool_calls")[0];
        Assert.Equal("generate_images", call.GetProperty("function").GetProperty("name").GetString());
        using var arguments = JsonDocument.Parse(call.GetProperty("function").GetProperty("arguments").GetString()!);
        Assert.Equal("a red square", arguments.RootElement.GetProperty("prompt").GetString());
    }

    [Fact]
    public async Task GenerateImage_ReturnsBase64Png()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        using var response = await client.PostAsJsonAsync("/v1/images/generations", new
        {
            model = "e2e/image",
            prompt = "a red square",
            n = 1,
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var bytes = Convert.FromBase64String(document.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString()!);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        Assert.Equal(64, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(64, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }
}
