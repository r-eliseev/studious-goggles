using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;

namespace ImageGen.Core.Providers;

/// <summary>
/// HTTP plumbing shared by the provider clients: absolute URLs from the config, the key from the secret store,
/// a per-request timeout from the config, and error mapping. Clients are singletons and never keep an
/// <see cref="HttpClient"/>: each request takes one from <see cref="IHttpClientFactory"/>.
/// </summary>
public abstract class ProviderClientBase(
    ProviderConfig config,
    IHttpClientFactory httpClientFactory,
    ISecretStore secretStore,
    TimeProvider timeProvider) : IProviderConnection
{
    public const string DefaultMediaType = "image/png";

    protected ProviderConfig Config { get; } = config;

    protected TimeProvider TimeProvider { get; } = timeProvider;

    /// <summary>Puts the key into the request (a bearer token or a provider-specific header).</summary>
    protected abstract void Authenticate(HttpRequestMessage request, string apiKey);

    public abstract Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken);

    /// <summary>GET or POST without a key; the caller authenticates the request.</summary>
    protected HttpRequestMessage CreateRequest(HttpMethod method, string path, JsonObject? body)
    {
        var request = new HttpRequestMessage(method, new Uri(Config.BaseAddress, path));
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        return request;
    }

    /// <summary>Request with the key from the secret store. Throws <see cref="ErrorKind.InvalidKey"/> when no key is saved.</summary>
    protected async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        HttpMethod method,
        string path,
        JsonObject? body,
        CancellationToken cancellationToken)
    {
        var apiKey = await secretStore.GetAsync(SecretNames.ForProvider(Config.Id), cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ProviderException(ErrorKind.InvalidKey, $"{Config.DisplayName} API key is not set.");
        }

        var request = CreateRequest(method, path, body);
        Authenticate(request, apiKey);
        return request;
    }

    /// <summary>Checks the key with a request that needs no body: 200 is valid, 401 and 403 are invalid, the rest is a network error.</summary>
    protected async Task<ApiKeyStatus> ProbeKeyAsync(HttpMethod method, string path, string apiKey, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, null);
        Authenticate(request, apiKey);
        try
        {
            using var timeout = CreateTimeout(cancellationToken);
            using var response = await httpClientFactory.CreateClient(Config.Id).SendAsync(request, timeout.Token);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => ApiKeyStatus.Valid,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ApiKeyStatus.Invalid,
                _ => ApiKeyStatus.NetworkError,
            };
        }
        catch (HttpRequestException)
        {
            return ApiKeyStatus.NetworkError;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiKeyStatus.NetworkError;
        }
    }

    /// <summary>Sends the request and returns the parsed JSON body. Provider errors become <see cref="ProviderException"/>.</summary>
    protected async Task<JsonNode> SendJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(cancellationToken);
        HttpResponseMessage response;
        string text;
        try
        {
            response = await httpClientFactory.CreateClient(Config.Id).SendAsync(request, timeout.Token);
            using (response)
            {
                text = await response.Content.ReadAsStringAsync(timeout.Token);
                return ParseResponse(response, text);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(ErrorKind.Network, $"Could not reach {Config.DisplayName}.", innerException: ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException(ErrorKind.Timeout, $"{Config.DisplayName} did not respond in time.", innerException: ex);
        }
    }

    /// <summary>Maps an error code (the HTTP status in practice) to <see cref="ErrorKind"/>.</summary>
    public static ErrorKind MapErrorKind(int code, bool hasModerationReasons) => code switch
    {
        400 => ErrorKind.BadRequest,
        401 => ErrorKind.InvalidKey,
        402 => ErrorKind.InsufficientFunds,
        403 when hasModerationReasons => ErrorKind.Moderated,
        403 => ErrorKind.Forbidden,
        408 => ErrorKind.Timeout,
        429 => ErrorKind.RateLimited,
        502 or 503 or 529 => ErrorKind.ModelUnavailable,
        _ => ErrorKind.Unknown,
    };

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(Config.Timeout);
        return source;
    }

    private JsonNode ParseResponse(HttpResponseMessage response, string text)
    {
        JsonNode? root = null;
        try
        {
            root = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            root = null;
        }

        var statusCode = (int)response.StatusCode;
        var error = root?["error"];
        if (!response.IsSuccessStatusCode || error is not null)
        {
            var code = error?["code"] is JsonValue codeValue && codeValue.TryGetValue<int>(out var number) ? number : statusCode;
            var message = error?["message"] is JsonValue messageValue && messageValue.TryGetValue<string>(out var messageText)
                && !string.IsNullOrWhiteSpace(messageText)
                ? messageText
                : $"{Config.DisplayName} returned HTTP {statusCode}.";
            var hasReasons = error?["metadata"]?["reasons"] is JsonArray;
            throw new ProviderException(MapErrorKind(code, hasReasons), message);
        }

        return root ?? throw new ProviderException(ErrorKind.EmptyResponse, $"{Config.DisplayName} returned an empty response.");
    }

    /// <summary>Bearer token, the usual form for OpenAI-style APIs.</summary>
    protected static void AuthenticateBearer(HttpRequestMessage request, string apiKey) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
}
