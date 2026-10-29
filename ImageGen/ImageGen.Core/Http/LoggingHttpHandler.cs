using System.Diagnostics;
using ImageGen.Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Http;

/// <summary>
/// Logs every request to a provider: method, path, status, duration and size. The query string, the headers
/// (the key is in <c>Authorization</c>) and the body are not logged at Information. At Debug the bodies are logged,
/// with image payloads replaced by their size (see <see cref="BodyRedactor"/>). When the debug trace is on (task 085),
/// the same traffic is recorded with headers from an allowlist and pictures stored as files (see <see cref="TraceRedaction"/>).
/// Added to every client from <see cref="IHttpClientFactory"/> by <c>AddImageGenCore</c>.
/// </summary>
public sealed class LoggingHttpHandler(ILogger<LoggingHttpHandler> logger, IDebugTrace trace) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method.Method;
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var started = Stopwatch.GetTimestamp();
        var traceId = Guid.NewGuid().ToString("N")[..8];

        if (request.Content is not null && (trace.IsEnabled || logger.IsEnabled(LogLevel.Debug)))
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }

        if (logger.IsEnabled(LogLevel.Debug) && request.Content is not null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            logger.LogDebug("Request body {Method} {Path}: {Body}", method, path, BodyRedactor.Redact(body));
        }

        if (trace.IsEnabled)
        {
            var blobs = new List<TraceBlob>();
            var body = request.Content is null ? null : TraceRedaction.ExtractImages(
                await request.Content.ReadAsStringAsync(cancellationToken), $"{traceId}-request", blobs);
            var headers = TraceRedaction.Headers(request.Content is null
                ? request.Headers
                : request.Headers.Concat(request.Content.Headers));
            trace.Record(new TraceRecord(
                "http-request",
                new Dictionary<string, string?>
                {
                    ["id"] = traceId,
                    ["method"] = method,
                    ["path"] = path,
                    ["headers"] = string.Join("; ", headers.Select(h => $"{h.Key}: {h.Value}")),
                    ["body"] = body,
                },
                blobs));
        }

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HTTP {Method} {Path} failed after {ElapsedMs} ms: network error.", method, path, ElapsedMs(started));
            throw;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "HTTP {Method} {Path} timed out after {ElapsedMs} ms.", method, path, ElapsedMs(started));
            throw;
        }

        // Reading the body here keeps it buffered, so the provider client can still read it afterwards.
        var size = (await response.Content.ReadAsByteArrayAsync(cancellationToken)).Length;
        logger.LogInformation(
            "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs} ms, {Size} bytes",
            method,
            path,
            (int)response.StatusCode,
            ElapsedMs(started),
            size);

        if (logger.IsEnabled(LogLevel.Debug) && !IsCredentialCheck(path))
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogDebug("Response body {Method} {Path}: {Body}", method, path, BodyRedactor.Redact(body));
        }

        // The key check is left out of the trace too: its body names the account.
        if (trace.IsEnabled && !IsCredentialCheck(path))
        {
            var blobs = new List<TraceBlob>();
            var body = TraceRedaction.ExtractImages(
                await response.Content.ReadAsStringAsync(cancellationToken), $"{traceId}-response", blobs);
            trace.Record(new TraceRecord(
                "http-response",
                new Dictionary<string, string?>
                {
                    ["id"] = traceId,
                    ["method"] = method,
                    ["path"] = path,
                    ["status"] = ((int)response.StatusCode).ToString(),
                    ["elapsedMs"] = ElapsedMs(started).ToString(),
                    ["headers"] = string.Join("; ", TraceRedaction.Headers(response.Headers.Concat(response.Content.Headers))
                        .Select(h => $"{h.Key}: {h.Value}")),
                    ["body"] = body,
                },
                blobs));
        }

        return response;
    }

    /// <summary>
    /// The key check (<c>GET /api/v1/key</c> at OpenRouter) answers with the key label and the account id.
    /// Its body is never logged, at any level; only the method, path, status and duration are.
    /// </summary>
    private static bool IsCredentialCheck(string path) => path.EndsWith("/key", StringComparison.Ordinal);

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
