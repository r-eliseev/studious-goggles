using System.Net;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Http;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Diagnostics;

/// <summary>
/// Task 085 acceptance: one generation through the real OpenRouter client is recorded by the trace, and a mock built only
/// from the recorded request and response gives the same result through the same client.
/// </summary>
public class DebugTraceProviderReplayTests
{
    private const string ApiKey = "sk-or-test-1234";
    private const string Picture = "iVBORw0KGgo=";
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenerateAsync_RecordedCall_MockFromTraceGivesSameImage()
    {
        var original = $"{{\"data\":[{{\"b64_json\":\"{Picture}\",\"media_type\":\"image/png\"}}]}}";
        var trace = new CapturingDebugTrace();
        var traced = CreateClient(new LoggingHttpHandler(NullLogger<LoggingHttpHandler>.Instance, trace)
        {
            InnerHandler = StubHttpMessageHandler.Json(HttpStatusCode.OK, original),
        });
        var request = new ImageGenerationRequest("openai/gpt-image-1", "a fox", Seed: 7, AspectRatio: "1:1");

        var first = await traced.GenerateAsync(request, TestContext.Current.CancellationToken);

        // Everything below uses only the trace, not the original test double.
        var requestRecord = Assert.Single(trace.Records, r => r.Kind == "http-request");
        var responseRecord = Assert.Single(trace.Records, r => r.Kind == "http-response");
        var blob = Assert.Single(responseRecord.Blobs);
        var replayBody = responseRecord.Fields["body"]!.Replace(
            $"blob:{blob.Name}", Convert.ToBase64String(blob.Content), StringComparison.Ordinal);
        var replayStatus = (HttpStatusCode)int.Parse(responseRecord.Fields["status"]!, System.Globalization.CultureInfo.InvariantCulture);
        var mock = StubHttpMessageHandler.Json(replayStatus, replayBody);

        var replayed = await CreateClient(mock).GenerateAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(first.Content, replayed.Content);
        Assert.Equal(first.MediaType, replayed.MediaType);
        Assert.Equal(Convert.FromBase64String(Picture), replayed.Content);
        Assert.Equal("POST", requestRecord.Fields["method"]);
        Assert.Equal("/api/v1/images", requestRecord.Fields["path"]);
        Assert.Equal("/api/v1/images", mock.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("\"prompt\":\"a fox\"", requestRecord.Fields["body"], StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, trace.AllText, StringComparison.Ordinal);
    }

    private static OpenRouterClient CreateClient(HttpMessageHandler handler)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(ApiKey);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.OpenRouter).Returns(new HttpClient(handler));
        var config = ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null).Get(ProviderIds.OpenRouter);
        return new OpenRouterClient(config, factory, secrets, time);
    }
}
