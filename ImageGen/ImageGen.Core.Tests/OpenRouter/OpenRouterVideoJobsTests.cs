using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;

namespace ImageGen.Core.Tests.OpenRouter;

public sealed class OpenRouterVideoJobsTests
{
    private const string ApiKey = "sk-or-test-1234";
    private const string PollingUrl = "https://openrouter.ai/api/v1/videos/job-123";
    private const string ContentUrl = "https://openrouter.ai/api/v1/videos/job-123/content?index=0";

    [Fact]
    public async Task SubmitAsync_TextToVideo_SendsSupportedFieldsAndBuildsHandle()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.Accepted, """
            {"id":"job-123","polling_url":"/api/v1/videos/job-123","status":"pending"}
            """);
        var provider = CreateClient(handler);

        var handle = await provider.SubmitAsync(new VideoGenerationRequest(
            "google/veo-3.1", "A quiet beach", 8, "1080p", "16:9", true, Seed: 42),
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.RequestBodies[0])!.AsObject();
        Assert.Equal("https://openrouter.ai/api/v1/videos", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("google/veo-3.1", body["model"]!.GetValue<string>());
        Assert.Equal("A quiet beach", body["prompt"]!.GetValue<string>());
        Assert.Equal(8, body["duration"]!.GetValue<int>());
        Assert.Equal("1080p", body["resolution"]!.GetValue<string>());
        Assert.Equal("16:9", body["aspect_ratio"]!.GetValue<string>());
        Assert.True(body["generate_audio"]!.GetValue<bool>());
        Assert.Equal(42, body["seed"]!.GetValue<long>());
        Assert.Equal(7, body.Count);
        Assert.Equal(new ProviderJobHandle("openrouter", "job-123", PollingUrl, ContentUrl, string.Empty), handle);
    }

    [Fact]
    public async Task SubmitAsync_FirstFrame_UsesDocumentedDataUrlShape()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.Accepted, """
            {"id":"job-123","polling_url":"/api/v1/videos/job-123","status":"pending"}
            """);
        var provider = CreateClient(handler);

        await provider.SubmitAsync(new VideoGenerationRequest(
            "google/veo-3.1", "Animate this", 8, null, null, false, null,
            new ImageReference([1, 2, 3], "image/png")), TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.RequestBodies[0])!.AsObject();
        var frame = Assert.Single(body["frame_images"]!.AsArray());
        Assert.Equal("image_url", frame!["type"]!.GetValue<string>());
        Assert.Equal("data:image/png;base64,AQID", frame["image_url"]!["url"]!.GetValue<string>());
        Assert.Equal("first_frame", frame["frame_type"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("https://evil.example/api/v1/videos/job-123")]
    [InlineData("http://openrouter.ai/api/v1/videos/job-123")]
    public async Task SubmitAsync_UntrustedPollingUrl_Throws(string pollingUrl)
    {
        var provider = CreateClient(StubHttpMessageHandler.Json(HttpStatusCode.Accepted,
            $$"""{"id":"job-123","polling_url":"{{pollingUrl}}"}"""));

        var error = await Assert.ThrowsAsync<ProviderException>(() => provider.SubmitAsync(
            new VideoGenerationRequest("google/veo-3.1", "Prompt", 8, null, null, false),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, error.Kind);
    }

    [Theory]
    [InlineData("pending", JobState.Queued)]
    [InlineData("in_progress", JobState.Running)]
    [InlineData("completed", JobState.Succeeded)]
    [InlineData("failed", JobState.Failed)]
    [InlineData("cancelled", JobState.Failed)]
    [InlineData("expired", JobState.Failed)]
    [InlineData("unexpected", JobState.Queued)]
    public async Task PollAsync_MapsStatusesAndUsage(string status, JobState expected)
    {
        var response = new JsonObject
        {
            ["status"] = status,
            ["usage"] = new JsonObject { ["cost"] = 0.4m },
        };
        if (status == "failed")
        {
            response["error"] = new JsonObject { ["code"] = 429, ["message"] = "rate limited" };
        }

        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, response.ToJsonString());
        var provider = CreateClient(handler);

        var update = await provider.PollAsync(Handle, TestContext.Current.CancellationToken);

        Assert.Equal(expected, update.State);
        Assert.Equal("Bearer " + ApiKey, handler.Requests[0].Headers.Authorization!.ToString());
        if (status == "completed")
        {
            Assert.Equal(0.4m, update.CostUsd);
        }
        else if (status == "failed")
        {
            Assert.Equal(ErrorKind.RateLimited, update.ErrorKind);
            Assert.Equal("rate limited", update.ErrorMessage);
        }
        else if (status is "cancelled" or "expired")
        {
            Assert.Equal(ErrorKind.Unknown, update.ErrorKind);
        }
    }

    [Fact]
    public async Task OpenContentAsync_ReturnsStreamingContentAndDisposesResponse()
    {
        var expectedBytes = Enumerable.Range(0, 2048).Select(value => (byte)(value % 256)).ToArray();
        var content = new TrackingContent(expectedBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        });
        var provider = CreateClient(handler);

        var result = await provider.OpenContentAsync(Handle, TestContext.Current.CancellationToken);
        await using (result)
        {
            await using var downloaded = new MemoryStream();
            await result.Content.CopyToAsync(downloaded, TestContext.Current.CancellationToken);
            Assert.Equal(expectedBytes, downloaded.ToArray());
            Assert.Equal("video/mp4", result.MediaType);
            Assert.Equal(ContentUrl, handler.Requests[0].RequestUri!.ToString());
            Assert.Equal("Bearer " + ApiKey, handler.Requests[0].Headers.Authorization!.ToString());
        }

        Assert.True(content.WasDisposed);
    }

    [Fact]
    public async Task PollAsync_UntrustedStoredContentUrl_ThrowsBeforeRequest()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");
        var provider = CreateClient(handler);
        var handle = Handle with { ResponseUrl = "https://evil.example/video.mp4" };

        var error = await Assert.ThrowsAsync<ProviderException>(() => provider.PollAsync(
            handle, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, error.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task OpenContentAsync_DeclaredContentTooLarge_ThrowsBeforeReading()
    {
        var content = new TrackingContent([1]);
        content.Headers.ContentLength = ProviderResponseLimits.Video + 1;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        var provider = CreateClient(handler);

        var error = await Assert.ThrowsAsync<ProviderException>(() => provider.OpenContentAsync(
            Handle, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, error.Kind);
        Assert.True(content.WasDisposed);
    }

    private static ProviderJobHandle Handle => new("openrouter", "job-123", PollingUrl, ContentUrl, string.Empty);

    private static OpenRouterClient CreateClient(StubHttpMessageHandler handler)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(ApiKey);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("openrouter").Returns(new HttpClient(handler));
        var config = ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null).Get("openrouter");
        return new OpenRouterClient(config, factory, secrets, TimeProvider.System);
    }

    private sealed class TrackingContent(byte[] bytes) : ByteArrayContent(bytes)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
