using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ImageGen.Core.Http;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Tests.Http;

public class LoggingHttpHandlerTests
{
    private const string SecretKey = "sk-or-test-123456";

    [Fact]
    public async Task SendAsync_ApiKeyInAuthorizationHeader_KeyNotInAnyLogEntry()
    {
        var logger = new CapturingLogger<LoggingHttpHandler>();
        using var client = Client(logger, StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/key");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(SecretKey, logger.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_Success_LogsInformationWithMethodPathAndStatus()
    {
        var logger = new CapturingLogger<LoggingHttpHandler>();
        using var client = Client(logger, StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));

        using var response = await client.PostAsync(
            "https://example.test/api/v1/images?debug=1",
            new StringContent("{}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("POST", entry.Message, StringComparison.Ordinal);
        Assert.Contains("/api/v1/images", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("debug=1", entry.Message, StringComparison.Ordinal);
        Assert.Contains("200", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_RateLimited_LogsStatusCode429AtInformation()
    {
        var logger = new CapturingLogger<LoggingHttpHandler>();
        using var client = Client(logger, StubHttpMessageHandler.Json((HttpStatusCode)429, "{}"));

        using var response = await client.GetAsync("https://example.test/api/v1/images/models", TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("429", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_NetworkError_LogsWarningAndRethrows()
    {
        var logger = new CapturingLogger<LoggingHttpHandler>();
        using var client = Client(logger, StubHttpMessageHandler.Throws(new HttpRequestException("no route")));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("https://example.test/api/v1/key", TestContext.Current.CancellationToken));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("/api/v1/key", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_DebugLevel_ReplacesBase64ImageInResponseWithPlaceholder()
    {
        var base64 = new string('A', 4000);
        var json = $"{{\"data\":[{{\"b64_json\":\"{base64}\"}}]}}";
        var logger = new CapturingLogger<LoggingHttpHandler>();
        using var client = Client(logger, StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        using var response = await client.GetAsync("https://example.test/api/v1/images", TestContext.Current.CancellationToken);

        Assert.DoesNotContain(base64, logger.AllText, StringComparison.Ordinal);
        Assert.Contains("<base64, 3000 bytes>", logger.AllText, StringComparison.Ordinal);
        // The body itself is untouched: the provider client still reads the image.
        Assert.Equal(json, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Redact_LongBody_TruncatesToLimit()
    {
        var text = new string('x', 10_000);

        var redacted = BodyRedactor.Redact(text);

        Assert.True(redacted.Length <= BodyRedactor.MaxLength + 64);
        Assert.Contains("truncated", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_DataUrl_ReplacesPayloadWithPlaceholder()
    {
        var text = "{\"url\":\"data:image/png;base64,iVBORw0KGgo=\"}";

        var redacted = BodyRedactor.Redact(text);

        Assert.DoesNotContain("iVBORw0KGgo=", redacted, StringComparison.Ordinal);
        Assert.Contains("<data url, image/png, 8 bytes>", redacted, StringComparison.Ordinal);
    }

    private static HttpClient Client(ILogger<LoggingHttpHandler> logger, HttpMessageHandler inner) =>
        new(new LoggingHttpHandler(logger) { InnerHandler = inner });
}
