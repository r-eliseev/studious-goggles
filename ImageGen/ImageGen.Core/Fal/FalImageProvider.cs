using System.Net;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
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
public sealed class FalImageProvider : ProviderClientBase, IQueuedImageProvider, IModelProvider
{
    internal const string TransportName = "ImageGen.Internal.FalTransport";

    public FalImageProvider(
        ProviderConfig config,
        IHttpClientFactory httpClientFactory,
        ISecretStore secretStore,
        TimeProvider timeProvider)
        : base(config, httpClientFactory, secretStore, timeProvider)
    {
    }

    protected override string TransportClientName => TransportName;

    protected override void ValidateTransportResponse(HttpResponseMessage response)
    {
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new ProviderException(ErrorKind.Unknown, "fal returned a redirect response.");
        }
    }

    /// <summary>
    /// Checks the key with the model list. The list is open without a key in fal's documentation, so an invalid key may
    /// still answer 200: this check is an assumption until a live key is tried (task 025).
    /// </summary>
    public override Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken) =>
        ProbeKeyAsync(HttpMethod.Get, ModelsAddress + "?limit=1", apiKey, cancellationToken);

    /// <summary>
    /// Lists the text-to-image models of fal, following the cursor. The seed support and the price are not in the list
    /// response, so they are not claimed: the seed is unsupported, the price is unknown (task 025 open questions).
    /// </summary>
    public async Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken)
    {
        var fetchedAt = TimeProvider.GetUtcNow();
        var result = new List<ImageModel>();
        string? cursor = null;
        do
        {
            var address = ModelsAddress + "?category=text-to-image&limit=100" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
            var message = await CreateAuthorizedRequestAsync(HttpMethod.Get, address, null, cancellationToken);
            var page = await SendFalJsonAsync(message, cancellationToken);
            foreach (var model in page["models"] as JsonArray ?? [])
            {
                var id = model?["endpoint_id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                result.Add(new ImageModel
                {
                    ProviderId = Config.Id,
                    Id = id,
                    Name = model?["metadata"]?["display_name"]?.GetValue<string>() ?? id,
                    SupportsSeed = false,
                    SupportsImageInput = false,
                    MaxImagesPerRequest = 1,
                    FetchedAt = fetchedAt,
                });
            }

            cursor = page["has_more"]?.GetValue<bool>() == true ? page["next_cursor"]?.GetValue<string>() : null;
        }
        while (cursor is not null);

        return result;
    }

    /// <summary>Submit, then wait for the job and return its image; a filtered or failed job throws like the other providers.</summary>
    public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var handle = await SubmitAsync(request, cancellationToken);
        await foreach (var update in ProviderJobPoller.WatchAsync(this, handle, PollMinInterval, PollMaxInterval, MaxNetworkFailures, cancellationToken))
        {
            switch (update.State)
            {
                case JobState.Succeeded:
                    return update.Result!;
                case JobState.Filtered:
                    throw new ProviderException(ErrorKind.Moderated, update.ErrorMessage ?? "The provider filtered this image.");
                case JobState.Failed:
                    throw new ProviderException(update.ErrorKind ?? ErrorKind.Unknown, update.ErrorMessage ?? "fal failed the job.");
                case JobState.Cancelled:
                    throw new ProviderException(ErrorKind.Unknown, "The fal job was cancelled.");
            }
        }

        throw new ProviderException(ErrorKind.EmptyResponse, "fal ended the job without a result.");
    }

    private const string ModelsAddress = "https://api.fal.ai/v1/models";
    private static readonly TimeSpan PollMinInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollMaxInterval = TimeSpan.FromSeconds(5);
    private const int MaxNetworkFailures = 5;

    protected override void Authenticate(HttpRequestMessage request, string apiKey) =>
        request.Headers.TryAddWithoutValidation("Authorization", "Key " + apiKey);

    public async Task<ProviderJobHandle> SubmitAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
    {
        var modelEndpoint = FalUrlValidator.ValidateModelEndpoint(request.ModelId);
        var body = new JsonObject { ["prompt"] = request.Prompt };
        if (request.Seed is { } seed)
        {
            body["seed"] = seed;
        }

        var message = await CreateAuthorizedRequestAsync(HttpMethod.Post, modelEndpoint, body, cancellationToken);
        var json = await SendJsonAsync(message, cancellationToken);
        return new ProviderJobHandle(
            Config.Id,
            Text(json, "request_id"),
            CallbackAddress(json, "status_url"),
            CallbackAddress(json, "response_url"),
            CallbackAddress(json, "cancel_url"));
    }

    public async Task<JobUpdate> PollAsync(ProviderJobHandle handle, CancellationToken cancellationToken)
    {
        handle = ValidateHandle(handle);
        var message = await CreateAuthorizedRequestAsync(HttpMethod.Get, AddLogsQuery(handle.StatusUrl), null, cancellationToken);
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
        handle = ValidateHandle(handle);
        using var message = await CreateAuthorizedRequestAsync(HttpMethod.Put, handle.CancelUrl, null, cancellationToken);
        using var timeout = CreateTimeout(cancellationToken);
        try
        {
            using var response = await CreateHttpClient().SendAsync(message, timeout.Token);
            ValidateTransportResponse(response);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => CancelOutcome.Cancelled,
                HttpStatusCode.Accepted => CancelOutcome.CancellationRequested,
                HttpStatusCode.BadRequest => CancelOutcome.AlreadyCompleted,
                HttpStatusCode.NotFound => CancelOutcome.NotFound,
                _ => throw new ProviderException(ErrorKind.Unknown, $"fal returned HTTP {(int)response.StatusCode} on cancel."),
            };
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(ErrorKind.Network, "Could not reach fal.", innerException: ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException(ErrorKind.Timeout, "fal did not respond in time.", innerException: ex);
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
        var url = ImageAddress(image);
        var mediaType = image["content_type"]?.GetValue<string>() ?? DefaultMediaType;
        return new JobUpdate(JobState.Succeeded, Result: new ImageGenerationResult(await DownloadAsync(url, cancellationToken), mediaType, null));
    }

    /// <summary>
    /// Reads a fal JSON body. A job's status carries <c>error</c> as a plain string, so the base class (which expects an
    /// object there) cannot read it: only the HTTP status is checked here.
    /// </summary>
    private async Task<JsonNode> SendFalJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(cancellationToken);
        try
        {
            using (request)
            using (var response = await CreateHttpClient().SendAsync(request, timeout.Token))
            {
                ValidateTransportResponse(response);
                var text = await response.Content.ReadAsStringAsync(timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    throw new ProviderException(MapStatus(response.StatusCode), $"fal returned HTTP {(int)response.StatusCode}.");
                }

                return JsonNode.Parse(text) ?? throw new ProviderException(ErrorKind.EmptyResponse, "fal returned an empty body.");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(ErrorKind.Network, "Could not reach fal.", innerException: ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException(ErrorKind.Timeout, "fal did not respond in time.", innerException: ex);
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
        using var timeout = CreateTimeout(cancellationToken);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await CreateHttpClient().SendAsync(request, timeout.Token);
            ValidateTransportResponse(response);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(timeout.Token);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException(ErrorKind.Network, "Could not download the image from fal.", innerException: ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException(ErrorKind.Timeout, "fal did not download the image in time.", innerException: ex);
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

    private ProviderJobHandle ValidateHandle(ProviderJobHandle handle)
    {
        if (!string.Equals(handle.ProviderId, Config.Id, StringComparison.Ordinal))
        {
            throw new ProviderException(ErrorKind.Unknown, "fal returned an invalid job handle.");
        }

        return handle with
        {
            StatusUrl = FalUrlValidator.ValidateCallback(handle.StatusUrl, Config.BaseAddress).AbsoluteUri,
            ResponseUrl = FalUrlValidator.ValidateCallback(handle.ResponseUrl, Config.BaseAddress).AbsoluteUri,
            CancelUrl = FalUrlValidator.ValidateCallback(handle.CancelUrl, Config.BaseAddress).AbsoluteUri,
        };
    }

    private static string AddLogsQuery(string value)
    {
        var uri = new Uri(value, UriKind.Absolute);
        var builder = new UriBuilder(uri);
        var parameters = builder.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(parameter => !string.Equals(Uri.UnescapeDataString(parameter.Split('=', 2)[0]), "logs", StringComparison.OrdinalIgnoreCase))
            .ToList();
        parameters.Add("logs=1");
        builder.Query = string.Join('&', parameters);
        return builder.Uri.AbsoluteUri;
    }

    private string CallbackAddress(JsonNode node, string name)
    {
        var value = ReadUrl(node, name);
        return FalUrlValidator.ValidateCallback(value, Config.BaseAddress).AbsoluteUri;
    }

    private static string ImageAddress(JsonNode node) => FalUrlValidator.ValidateImage(ReadUrl(node, "url")).AbsoluteUri;

    private static string ReadUrl(JsonNode node, string name) =>
        node[name] is JsonValue value && value.TryGetValue<string>(out var address) && !string.IsNullOrWhiteSpace(address)
            ? address
            : throw new ProviderException(ErrorKind.Unknown, "fal returned an invalid URL or model endpoint.");

    private static int? Integer(JsonNode node, string name) => node[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static string Text(JsonNode node, string name) =>
        node[name]?.GetValue<string>() ?? throw new ProviderException(ErrorKind.EmptyResponse, $"fal response has no '{name}'.");
}
