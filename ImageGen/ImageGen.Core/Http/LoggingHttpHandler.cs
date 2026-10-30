using System.Diagnostics;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Providers;
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

        var inspectRequestBody = request.Content is not null && (trace.IsEnabled || logger.IsEnabled(LogLevel.Debug));
        string? requestBody = null;
        if (inspectRequestBody)
        {
            var bytes = await BoundedHttpContentReader.ReadBytesAsync(request.Content!, ProviderResponseLimits.RequestBody, cancellationToken);
            request.Content = ReplaceContent(request.Content!, bytes);
            requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        if (logger.IsEnabled(LogLevel.Debug) && requestBody is not null)
        {
            logger.LogDebug("Request body {Method} {Path}: {Body}", method, path, BodyRedactor.Redact(requestBody));
        }

        if (trace.IsEnabled)
        {
            var blobs = new List<TraceBlob>();
            var body = requestBody is null ? null : TraceRedaction.ExtractImages(requestBody, $"{traceId}-request", blobs);
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
            logger.LogWarning("HTTP {Method} {Path} failed after {ElapsedMs} ms: network error.", method, path, ElapsedMs(started));
            logger.LogDebug(ex, "HTTP request detail {Method} {Path}", method, path);
            throw;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("HTTP {Method} {Path} timed out after {ElapsedMs} ms.", method, path, ElapsedMs(started));
            logger.LogDebug(ex, "HTTP request detail {Method} {Path}", method, path);
            throw;
        }

        long? size = response.Content.Headers.ContentLength;
        var skipBody = request.Options.TryGetValue(ProviderResponseLimits.SkipResponseBody, out var skip) && skip;
        var isCredentialCheck = IsCredentialCheck(path);
        var inspectResponseBody = !skipBody && !isCredentialCheck && (trace.IsEnabled || logger.IsEnabled(LogLevel.Debug));
        if (inspectResponseBody)
        {
            try
            {
                var bytes = await BoundedHttpContentReader.ReadBytesAsync(response.Content, ProviderResponseLimits.GetLimit(request), cancellationToken);
                size = bytes.LongLength;
                response.Content = ReplaceContent(response.Content, bytes);
            }
            catch (HttpRequestException ex)
            {
                response.Dispose();
                logger.LogWarning("HTTP {Method} {Path} failed after {ElapsedMs} ms: network error.", method, path, ElapsedMs(started));
                logger.LogDebug(ex, "HTTP response detail {Method} {Path}", method, path);
                throw;
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                response.Dispose();
                logger.LogWarning("HTTP {Method} {Path} timed out after {ElapsedMs} ms.", method, path, ElapsedMs(started));
                logger.LogDebug(ex, "HTTP response detail {Method} {Path}", method, path);
                throw;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }

        logger.LogInformation(
            "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs} ms, {Size}",
            method,
            path,
            (int)response.StatusCode,
            ElapsedMs(started),
            size.HasValue ? $"{size.Value} bytes" : "unknown size");

        if (logger.IsEnabled(LogLevel.Debug) && inspectResponseBody)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogDebug("Response body {Method} {Path}: {Body}", method, path, BodyRedactor.Redact(body));
        }

        // The key check is left out of the trace too: its body names the account.
        if (trace.IsEnabled && inspectResponseBody)
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

    private static HttpContent ReplaceContent(HttpContent original, byte[] bytes)
    {
        var replacement = new ByteArrayContent(bytes);
        foreach (var header in original.Headers)
        {
            replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        original.Dispose();
        return replacement;
    }

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
