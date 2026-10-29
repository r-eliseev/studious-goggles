using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Http;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Diagnostics;

public class DebugTraceHandlerTests
{
    private const string SecretKey = "sk-or-test-123456";
    private const string Picture = "iVBORw0KGgo=";

    [Fact]
    public async Task SendAsync_TraceEnabled_RecordsRequestAndResponseWithoutKey()
    {
        var trace = new CapturingDebugTrace();
        using var client = Client(trace, StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"ok\":true}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/api/v1/chat");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);
        request.Content = new StringContent("{\"prompt\":\"cat\"}", Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(["http-request", "http-response"], trace.Records.Select(r => r.Kind));
        var requestRecord = trace.Records[0];
        Assert.Equal("POST", requestRecord.Fields["method"]);
        Assert.Equal("/api/v1/chat", requestRecord.Fields["path"]);
        Assert.Equal("{\"prompt\":\"cat\"}", requestRecord.Fields["body"]);
        Assert.Equal("200", trace.Records[1].Fields["status"]);
        Assert.DoesNotContain(SecretKey, trace.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", trace.AllText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_PictureInResponse_StoredAsBlobNotInline()
    {
        var trace = new CapturingDebugTrace();
        var body = $"{{\"url\":\"data:image/png;base64,{Picture}\"}}";
        using var client = Client(trace, StubHttpMessageHandler.Json(HttpStatusCode.OK, body));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/image");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var record = Assert.Single(trace.Records, r => r.Kind == "http-response");
        var blob = Assert.Single(record.Blobs);
        Assert.Equal("image/png", blob.MediaType);
        Assert.Equal(Convert.FromBase64String(Picture), blob.Content);
        Assert.Contains($"blob:{blob.Name}", record.Fields["body"], StringComparison.Ordinal);
        Assert.DoesNotContain(Picture, trace.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Replay_RecordedResponseRebuiltFromTrace_ReproducesStatusAndBody()
    {
        // The trace of a provider response is enough to build the provider's mock: the picture comes back from its blob.
        var original = $"{{\"url\":\"data:image/png;base64,{Picture}\"}}";
        var trace = new CapturingDebugTrace();
        using (var client = Client(trace, StubHttpMessageHandler.Json(HttpStatusCode.OK, original)))
        using (var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/image"))
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var record = Assert.Single(trace.Records, r => r.Kind == "http-response");
        var blob = Assert.Single(record.Blobs);
        var recordedBody = record.Fields["body"]!.Replace($"blob:{blob.Name}", $"data:{blob.MediaType};base64,{Convert.ToBase64String(blob.Content)}", StringComparison.Ordinal);
        var status = (HttpStatusCode)int.Parse(record.Fields["status"]!);

        using var mock = new HttpClient(StubHttpMessageHandler.Json(status, recordedBody));
        var replayed = await mock.GetStringAsync("https://example.test/api/v1/image", TestContext.Current.CancellationToken);

        Assert.Equal(original, replayed);
        Assert.Equal("200", record.Fields["status"]);
    }

    [Fact]
    public async Task SendAsync_CredentialCheck_ResponseNotRecorded()
    {
        var trace = new CapturingDebugTrace();
        using var client = Client(trace, StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"label\":\"account-label\"}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/key");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Single(trace.Records, r => r.Kind == "http-request");
        Assert.DoesNotContain(trace.Records, r => r.Kind == "http-response");
        Assert.DoesNotContain("account-label", trace.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_TraceDisabled_RecordsNothing()
    {
        var trace = new CapturingDebugTrace { IsEnabled = false };
        using var client = Client(trace, StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/v1/models");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Empty(trace.Records);
    }

    [Fact]
    public void ExtractImages_InvalidBase64_NoBlobAndKeptAsText()
    {
        var blobs = new List<TraceBlob>();

        var text = TraceRedaction.ExtractImages("data:image/png;base64,abc", "p", blobs);

        Assert.Empty(blobs);
        Assert.Contains("data:image/png;base64,abc", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractImages_B64JsonField_BlobAndReferenceKeepBodyWhole()
    {
        var blobs = new List<TraceBlob>();
        var body = $"{{\"data\":[{{\"b64_json\":\"{Picture}\"}}],\"note\":\"{new string('x', 5000)}\"}}";

        var text = TraceRedaction.ExtractImages(body, "p", blobs);

        var blob = Assert.Single(blobs);
        Assert.Equal("application/octet-stream", blob.MediaType);
        Assert.Equal(Convert.FromBase64String(Picture), blob.Content);
        Assert.Contains($"\"b64_json\":\"blob:{blob.Name}\"", text, StringComparison.Ordinal);
        Assert.Contains(new string('x', 5000), text, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractImages_AccountFields_Redacted()
    {
        var text = TraceRedaction.ExtractImages("{\"label\":\"my-account\",\"ok\":1}", "p", []);

        Assert.DoesNotContain("my-account", text, StringComparison.Ordinal);
        Assert.Contains("\"ok\":1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Headers_OnlyAllowlistedKept()
    {
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            ["Authorization"] = ["Bearer x"],
            ["x-api-key"] = ["k"],
            ["Content-Type"] = ["application/json"],
        };

        var kept = TraceRedaction.Headers(headers);

        Assert.Equal(["Content-Type"], kept.Keys);
    }

    private static HttpClient Client(IDebugTrace trace, HttpMessageHandler inner) =>
        new(new LoggingHttpHandler(NullLogger<LoggingHttpHandler>.Instance, trace) { InnerHandler = inner });
}

/// <summary>Keeps every record in memory and renders them as text for "must not contain" checks.</summary>
internal sealed class CapturingDebugTrace : IDebugTrace
{
    public bool IsEnabled { get; init; } = true;

    public List<TraceRecord> Records { get; } = [];

    public string AllText => string.Join("\n", Records.Select(r =>
        string.Join(" ", r.Fields.Select(f => $"{f.Key}={f.Value}"))));

    public void Record(TraceRecord record) => Records.Add(record);
}
