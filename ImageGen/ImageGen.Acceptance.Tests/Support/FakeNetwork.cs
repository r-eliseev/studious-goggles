using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace ImageGen.Acceptance.Tests.Support;

public sealed class FakeNetwork : HttpMessageHandler
{
    private readonly ConcurrentDictionary<RouteKey, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _routes = new();
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private readonly ConcurrentDictionary<string, byte> _secrets = new(StringComparer.Ordinal);

    public IReadOnlyList<RecordedRequest> Requests => _requests.ToArray();

    public void RegisterSecret(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        _secrets.TryAdd(secret, 0);
    }

    public void AddRoute(
        HttpMethod method,
        string host,
        string path,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(respond);
        var key = new RouteKey(method.Method.ToUpperInvariant(), host.ToLowerInvariant(), path);
        if (!_routes.TryAdd(key, respond))
        {
            throw new InvalidOperationException($"A fake route is already registered for {method.Method} {host}{path}.");
        }
    }

    public void AddResponse(HttpMethod method, string host, string path, HttpStatusCode statusCode, string body, string mediaType = "application/json") =>
        AddRoute(method, host, path, (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        }));

    public HeldResponse AddHeldResponse(HttpMethod method, string host, string path)
    {
        var held = new HeldResponse();
        AddRoute(method, host, path, (_, cancellationToken) => held.WaitAsync(cancellationToken));
        return held;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("A fake request has no URI.");
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var recorded = new RecordedRequest(request.Method.Method, RedactAddress(uri), RedactBody(body));
        _requests.Enqueue(recorded);

        var key = new RouteKey(request.Method.Method.ToUpperInvariant(), uri.Host.ToLowerInvariant(), uri.AbsolutePath);
        if (!_routes.TryGetValue(key, out var respond))
        {
            var match = _routes.FirstOrDefault(route =>
                route.Key.Method == key.Method
                && route.Key.Host == key.Host
                && MatchesPath(route.Key.Path, key.Path));
            respond = match.Value;
            if (respond is null)
            {
                throw new InvalidOperationException($"Unexpected request {recorded.Method} {recorded.Address}.");
            }
        }

        return await respond(request, cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing) { }

    private string RedactAddress(Uri uri)
    {
        var builder = new UriBuilder(uri);
        var query = uri.Query.TrimStart('?');
        if (query.Length > 0)
        {
            var safeParts = query.Split('&').Select(part =>
            {
                var separator = part.IndexOf('=');
                if (separator < 0) return RedactSecrets(part);
                var name = Uri.UnescapeDataString(part[..separator]);
                var value = part[(separator + 1)..];
                return IsSensitiveName(name) ? part[..separator] + "=%3Credacted%3E" : RedactSecrets(part[..separator] + "=" + value);
            });
            builder.Query = string.Join("&", safeParts);
        }

        return RedactSecrets(builder.Uri.AbsoluteUri);
    }

    private string? RedactBody(string? body)
    {
        if (body is null) return null;
        var text = RedactSecrets(body);
        try
        {
            var node = JsonNode.Parse(text);
            RedactJson(node);
            return node?.ToJsonString() ?? text;
        }
        catch (System.Text.Json.JsonException)
        {
            return text;
        }
    }

    private string RedactSecrets(string value)
    {
        foreach (var secret in _secrets.Keys)
        {
            value = value.Replace(secret, "<redacted>", StringComparison.Ordinal);
        }

        return value;
    }

    private static void RedactJson(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToArray())
            {
                if (IsSensitiveName(property.Key)) jsonObject[property.Key] = "<redacted>";
                else RedactJson(property.Value);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray) RedactJson(item);
        }
    }

    private static bool IsSensitiveName(string name)
    {
        var normalized = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized is "key" or "apikey" or "token" or "authorization";
    }

    private static bool MatchesPath(string pattern, string path)
    {
        var wildcard = pattern.IndexOf('*');
        if (wildcard < 0) return pattern == path;
        var prefix = pattern[..wildcard];
        var suffix = pattern[(wildcard + 1)..];
        return path.StartsWith(prefix, StringComparison.Ordinal)
            && path.EndsWith(suffix, StringComparison.Ordinal)
            && path.Length >= prefix.Length + suffix.Length;
    }

    private readonly record struct RouteKey(string Method, string Host, string Path);

    public sealed record RecordedRequest(string Method, string Address, string? Body);

    public sealed class HeldResponse
    {
        private readonly TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<HttpResponseMessage> _response = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitUntilRequestedAsync(CancellationToken cancellationToken) => _requested.Task.WaitAsync(cancellationToken);

        public void Release(HttpStatusCode statusCode, string body, string mediaType = "application/json") =>
            _response.TrySetResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, mediaType) });

        internal async Task<HttpResponseMessage> WaitAsync(CancellationToken cancellationToken)
        {
            _requested.TrySetResult();
            return await _response.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
