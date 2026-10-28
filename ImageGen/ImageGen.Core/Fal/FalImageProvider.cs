using System.Net;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Fal;

/// <summary>
/// fal image provider on the queue API (ADR 0003): submit returns a handle with the addresses from the response; the state
/// is read from the status address, the result from the response address, a job is cancelled at the cancel address.
/// The endpoint is the model id. The response shape of a model is taken from the fal documentation for the queue;
/// the fields of the image result (<c>images</c>, <c>has_nsfw_concepts</c>) are the planned ones (task 025, not verified
/// with a live key).
/// </summary>
public sealed class FalImageProvider : ProviderClientBase, IQueuedImageProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public FalImageProvider(
        ProviderConfig config,
        IHttpClientFactory httpClientFactory,
        ISecretStore secretStore,
        TimeProvider timeProvider)
        : base(config, httpClientFactory, secretStore, timeProvider)
    {
        _httpClientFactory = httpClientFactory;
    }

    public override Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken) =>
        // The key check address of fal is not in the documentation read for task 025 (open question 2 in the task).
        throw new ProviderException(ErrorKind.ModelUnavailable, "Checking a fal key is not supported yet.");

    protected override void Authenticate(HttpRequestMessage request, string apiKey) =>
        request.Headers.TryAddWithoutValidation("Authorization", "Key " + apiKey);

    public async Task<ProviderJobHandle> SubmitAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["prompt"] = request.Prompt };
        if (request.Seed is { } seed)
        {
            body["seed"] = seed;
        }

        var message = await CreateAuthorizedRequestAsync(HttpMethod.Post, request.ModelId, body, cancellationToken);
        var json = await SendJsonAsync(message, cancellationToken);
        return new ProviderJobHandle(
            Config.Id,
            Text(json, "request_id"),
            Text(json, "status_url"),
            Text(json, "response_url"),
            Text(json, "cancel_url"));
    }

    public async Task<JobUpdate> PollAsync(ProviderJobHandle handle, CancellationToken cancellationToken)
    {
        var message = await CreateAuthorizedRequestAsync(HttpMethod.Get, handle.StatusUrl + "?logs=1", null, cancellationToken);
        var status = await SendFalJsonAsync(message, cancellationToken);
        return status["status"]?.GetValue<string>() switch
        {
            "IN_QUEUE" => new JobUpdate(JobState.Queued, QueuePosition: Integer(status, "queue_position")),
            "IN_PROGRESS" => new JobUpdate(JobState.Running, Log: JoinLogs(status)),
            "COMPLETED" => await CompletedAsync(status, handle, cancellationToken),
            _ => new JobUpdate(JobState.Queued),
        };
    }

    public async Task<CancelOutcome> CancelAsync(ProviderJobHandle handle, CancellationToken cancellationToken)
    {
        var message = await CreateAuthorizedRequestAsync(HttpMethod.Put, handle.CancelUrl, null, cancellationToken);
        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory.CreateClient(Config.Id).SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(ErrorKind.Network, "Could not reach fal.", innerException: ex);
        }

        using (response)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.OK => CancelOutcome.Cancelled,
                HttpStatusCode.Accepted => CancelOutcome.CancellationRequested,
                HttpStatusCode.BadRequest => CancelOutcome.AlreadyCompleted,
                HttpStatusCode.NotFound => CancelOutcome.NotFound,
                _ => throw new ProviderException(ErrorKind.Unknown, $"fal returned HTTP {(int)response.StatusCode} on cancel."),
            };
        }
    }

    private async Task<JobUpdate> CompletedAsync(JsonNode status, ProviderJobHandle handle, CancellationToken cancellationToken)
    {
        var error = status["error"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(error))
        {
            return new JobUpdate(JobState.Failed, ErrorKind: MapErrorType(status["error_type"]?.GetValue<string>()), ErrorMessage: error);
        }

        var message = await CreateAuthorizedRequestAsync(HttpMethod.Get, handle.ResponseUrl, null, cancellationToken);
        var result = await SendFalJsonAsync(message, cancellationToken);
        if (result["has_nsfw_concepts"] is JsonArray flags && flags.Any(flag => flag?.GetValue<bool>() == true))
        {
            return new JobUpdate(JobState.Filtered, ErrorKind: ErrorKind.Moderated, ErrorMessage: "The provider filtered this image.");
        }

        var image = result["images"]?[0] ?? throw new ProviderException(ErrorKind.EmptyResponse, "fal returned no image.");
        var url = Text(image, "url");
        var mediaType = image["content_type"]?.GetValue<string>() ?? DefaultMediaType;
        return new JobUpdate(JobState.Succeeded, Result: new ImageGenerationResult(await DownloadAsync(url, cancellationToken), mediaType, null));
    }

    /// <summary>
    /// Reads a fal JSON body. A job's status carries <c>error</c> as a plain string, so the base class (which expects an
    /// object there) cannot read it: only the HTTP status is checked here.
    /// </summary>
    private async Task<JsonNode> SendFalJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory.CreateClient(Config.Id).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(ErrorKind.Network, "Could not reach fal.", innerException: ex);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new ProviderException(MapStatus(response.StatusCode), $"fal returned HTTP {(int)response.StatusCode}.");
            }

            return JsonNode.Parse(text) ?? throw new ProviderException(ErrorKind.EmptyResponse, "fal returned an empty body.");
        }
    }

    /// <summary>HTTP status to the app's error kinds. Assumed like the other providers until fal's error body is checked live.</summary>
    private static ErrorKind MapStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ErrorKind.InvalidKey,
        HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => ErrorKind.BadRequest,
        HttpStatusCode.TooManyRequests => ErrorKind.RateLimited,
        HttpStatusCode.NotFound => ErrorKind.ModelUnavailable,
        _ when (int)status >= 500 => ErrorKind.Network,
        _ => ErrorKind.Unknown,
    };

    private async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClientFactory.CreateClient(Config.Id).SendAsync(new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(ErrorKind.Network, "Could not download the image from fal.", innerException: ex);
        }
    }

    /// <summary>Error types of fal mapped to the app's kinds. The mapping is an assumption until a real error is seen.</summary>
    private static ErrorKind MapErrorType(string? type) => type switch
    {
        "validation_error" => ErrorKind.BadRequest,
        "timeout" => ErrorKind.Timeout,
        _ => ErrorKind.Unknown,
    };

    private static string JoinLogs(JsonNode status) =>
        string.Join('\n', (status["logs"] as JsonArray)?.Select(log => log?["message"]?.GetValue<string>()).OfType<string>() ?? []);

    private static int? Integer(JsonNode node, string name) => node[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static string Text(JsonNode node, string name) =>
        node[name]?.GetValue<string>() ?? throw new ProviderException(ErrorKind.EmptyResponse, $"fal response has no '{name}'.");
}
