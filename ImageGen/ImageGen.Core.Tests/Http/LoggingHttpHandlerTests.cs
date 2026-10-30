using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Http;
using ImageGen.Core.Providers;
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
    public async Task SendAsync_Information_DoesNotReadResponseBody()
    {
        var content = new CountingContent([1, 2, 3]);
        var logger = new CapturingLogger<LoggingHttpHandler>(LogLevel.Information);
        using var client = Client(logger, new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/models");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        Assert.Equal(0, content.BytesRead);
        Assert.Contains("3 bytes", logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_SkipResponseBody_DoesNotReadBodyEvenAtDebug()
    {
        var content = new CountingContent([1, 2, 3]);
        var logger = new CapturingLogger<LoggingHttpHandler>(LogLevel.Debug);
        using var client = Client(logger, new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/key");
        request.Options.Set(ProviderResponseLimits.SkipResponseBody, true);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        Assert.Equal(0, content.BytesRead);
    }

    [Fact]
    public async Task SendAsync_DebugResponse_RemainsReadableByCaller()
    {
        const string body = "{\"ok\":true}";
        var logger = new CapturingLogger<LoggingHttpHandler>(LogLevel.Debug);
        using var client = Client(logger, StubHttpMessageHandler.Json(HttpStatusCode.OK, body));

        using var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/models"),
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken);

        Assert.Equal(body, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(body, logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_DebugOversizedResponse_ThrowsWithoutPartialBodyLog()
    {
        var content = new CountingContent([]);
        content.Headers.ContentLength = ProviderResponseLimits.Json + 1;
        var logger = new CapturingLogger<LoggingHttpHandler>(LogLevel.Debug);
        using var client = Client(logger, new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        await Assert.ThrowsAsync<ProviderResponseTooLargeException>(() => client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/models"),
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken));

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("Response body", StringComparison.Ordinal));
        Assert.Equal(0, content.BytesRead);
    }

    [Fact]
    public async Task SendAsync_DebugOversizedRequest_StopsBeforeTransportAndDoesNotLogPartialBody()
    {
        var content = new CountingContent([]);
        content.Headers.ContentLength = ProviderResponseLimits.RequestBody + 1;
        var logger = new CapturingLogger<LoggingHttpHandler>(LogLevel.Debug);
        var inner = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = Client(logger, inner);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/api/v1/images") { Content = content };

        await Assert.ThrowsAsync<ProviderResponseTooLargeException>(() => client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken));

        Assert.Empty(inner.Requests);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("Request body", StringComparison.Ordinal));
        Assert.Equal(0, content.BytesRead);
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

        var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("/api/v1/key", entry.Message, StringComparison.Ordinal);
        Assert.Null(entry.Exception);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Debug && e.Exception is HttpRequestException);
    }

    [Fact]
    public async Task SendAsync_DebugLevel_ReplacesBase64ImageInResponseWithPlaceholder()
    {
        var base64 = new string('A', 4000);
        var json = $"{{\"data\":[{{\"b64_json\":\"{base64}\"}}]}}";
        var logger = new CapturingLogger<LoggingHttpHandler>();
        using var client = Client(logger, StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/images"), HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(base64, logger.AllText, StringComparison.Ordinal);
        Assert.Contains("<base64, 3000 bytes>", logger.AllText, StringComparison.Ordinal);
        // The body itself is untouched: the provider client still reads the image.
        Assert.Equal(json, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_KeyCheckResponse_BodyIsNotLoggedAtDebug()
    {
        const string json = "{\"data\":{\"label\":\"sk-or-v1-0d5secret\",\"creator_user_id\":\"user_abc123\"}}";
        var logger = new CapturingLogger<LoggingHttpHandler>();
        using var client = Client(logger, StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        using var response = await client.GetAsync("https://example.test/api/v1/key", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("sk-or-v1-0d5secret", logger.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("user_abc123", logger.AllText, StringComparison.Ordinal);
        Assert.Contains("/api/v1/key", logger.AllText, StringComparison.Ordinal);
        Assert.Equal(json, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Redact_ApiKeyAndUserIdFields_ReplacesValuesWithPlaceholder()
    {
        var text = "{\"api_key\":\"sk-or-secret-1\",\"creator_user_id\":\"user_2\",\"model\":\"gemma\"}";

        var redacted = BodyRedactor.Redact(text);

        Assert.DoesNotContain("sk-or-secret-1", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("user_2", redacted, StringComparison.Ordinal);
        Assert.Contains("\"api_key\":\"<redacted>\"", redacted, StringComparison.Ordinal);
        Assert.Contains("\"creator_user_id\":\"<redacted>\"", redacted, StringComparison.Ordinal);
        Assert.Contains("\"model\":\"gemma\"", redacted, StringComparison.Ordinal);
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
        new(new LoggingHttpHandler(logger, NullDebugTrace.Instance) { InnerHandler = inner });

    private sealed class CountingContent(byte[] bytes) : HttpContent
    {
        public int BytesRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();

        protected override bool TryComputeLength(out long length)
        {
            length = bytes.Length;
            return true;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new CountingStream(bytes, this));

        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new CountingStream(bytes, this));

        private sealed class CountingStream(byte[] bytes, CountingContent owner) : MemoryStream(bytes, writable: false)
        {
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var read = base.ReadAsync(buffer, cancellationToken);
                owner.BytesRead += read.Result;
                return read;
            }
        }
    }
}
