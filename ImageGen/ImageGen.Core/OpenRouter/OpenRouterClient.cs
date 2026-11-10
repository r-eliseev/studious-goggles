using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.OpenAiCompatible;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.OpenRouter;

/// <summary>OpenRouter: images through <c>images</c> and <c>images/models</c>, chat through <c>chat/completions</c>.</summary>
public sealed class OpenRouterClient(
    ProviderConfig config,
    IHttpClientFactory httpClientFactory,
    ISecretStore secretStore,
    TimeProvider timeProvider,
    ILogger<OpenRouterClient>? logger = null)
    : ProviderClientBase(config, httpClientFactory, secretStore, timeProvider), IModelProvider, IAssistantChatClient, ISynchronousImageTransport, ITextEmbeddingClient, IVideoModelProvider, IEmbeddingModelProvider, IQueuedVideoProvider, IAudioModelProvider, IAudioGenerationProvider
{
    private const int PriceRequestParallelism = 4;
    private readonly ILogger<OpenRouterClient> _logger = logger ?? NullLogger<OpenRouterClient>.Instance;

    protected override void Authenticate(HttpRequestMessage request, string apiKey) => AuthenticateBearer(request, apiKey);

    public override Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return ProbeKeyAsync(HttpMethod.Get, "key", apiKey, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "images/models", null, cancellationToken);
        var root = await SendJsonAsync(request, cancellationToken);
        var fetchedAt = TimeProvider.GetUtcNow();
        var result = new List<ImageModel>();
        if (root["data"] is not JsonArray data)
        {
            return result;
        }

        foreach (var item in data)
        {
            var id = item?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var parameters = item!["supported_parameters"] as JsonObject;
            var inputModalities = item["architecture"]?["input_modalities"] as JsonArray;
            result.Add(new ImageModel
            {
                ProviderId = Config.Id,
                Id = id,
                Name = item["name"]?.GetValue<string>() ?? id,
                SupportsSeed = parameters?.ContainsKey("seed") == true,
                SupportsImageInput = inputModalities?.Any(m => m?.GetValue<string>() == "image") == true,
                MaxImagesPerRequest = ReadMaxImages(parameters),
                Parameters = ReadParameters(parameters),
                FetchedAt = fetchedAt,
            });
        }

        await Parallel.ForEachAsync(
            result,
            new ParallelOptions { MaxDegreeOfParallelism = PriceRequestParallelism, CancellationToken = cancellationToken },
            async (model, token) => model.Prices = await ReadPricesAsync(model.Id, token));

        return result;
    }

    public async Task<IReadOnlyList<VideoModel>> ListVideoModelsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "videos/models", null, cancellationToken);
        var root = await SendJsonAsync(request, cancellationToken);
        var fetchedAt = TimeProvider.GetUtcNow();
        var result = new List<VideoModel>();
        if (root["data"] is not JsonArray data)
        {
            return result;
        }

        foreach (var item in data.OfType<JsonObject>())
        {
            var id = ReadString(item["id"]);
            var durations = ReadVideoIntegers(item["supported_durations"]);
            if (string.IsNullOrWhiteSpace(id) || durations.Count == 0)
            {
                continue;
            }

            result.Add(new VideoModel
            {
                ProviderId = Config.Id,
                Id = id,
                Name = ReadString(item["name"]) ?? id,
                Durations = durations,
                Resolutions = ReadVideoStrings(item["supported_resolutions"]),
                AspectRatios = ReadVideoStrings(item["supported_aspect_ratios"]),
                FrameImages = ReadVideoStrings(item["supported_frame_images"]),
                GeneratesAudio = ReadNullableBoolean(item["generate_audio"]),
                SupportsSeed = ReadNullableBoolean(item["seed"]),
                PriceSkus = ReadVideoPrices(item["pricing_skus"], id),
                FetchedAt = fetchedAt,
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<AudioModel>> ListAudioModelsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "models?output_modalities=all", null, cancellationToken);
        var root = await SendJsonAsync(request, cancellationToken);
        var fetchedAt = TimeProvider.GetUtcNow();
        var result = new List<AudioModel>();
        if (root["data"] is not JsonArray data)
        {
            return result;
        }

        foreach (var item in data.OfType<JsonObject>())
        {
            var id = ReadString(item["id"]);
            var modalities = item["architecture"]?["output_modalities"] as JsonArray;
            if (string.IsNullOrWhiteSpace(id) || modalities is null)
            {
                continue;
            }

            var hasAudio = modalities.Any(value => ReadString(value) == "audio");
            var hasSpeech = modalities.Any(value => ReadString(value) == "speech");
            if (!hasAudio && !hasSpeech)
            {
                continue;
            }

            var output = hasAudio ? AudioOutput.ChatAudio : AudioOutput.Speech;
            result.Add(new AudioModel
            {
                ProviderId = Config.Id,
                Id = id,
                Name = ReadString(item["name"]) ?? id,
                Description = ReadString(item["description"]),
                Output = output,
                PricePerInputCharacterUsd = output == AudioOutput.Speech ? ReadPrice(item["pricing"]?["prompt"]) : null,
                FetchedAt = fetchedAt,
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<EmbeddingModel>> ListEmbeddingModelsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = CreateRequest(HttpMethod.Get, "embeddings/models", null);
        var root = await SendJsonAsync(request, cancellationToken);
        var fetchedAt = TimeProvider.GetUtcNow();
        var result = new List<EmbeddingModel>();
        if (root["data"] is not JsonArray data)
        {
            return result;
        }

        foreach (var item in data.OfType<JsonObject>())
        {
            var id = ReadString(item["id"]);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            result.Add(new EmbeddingModel
            {
                ProviderId = Config.Id,
                Id = id,
                Name = ReadString(item["name"]) ?? id,
                Description = ReadString(item["description"]) ?? string.Empty,
                ContextLength = ReadInt(item["context_length"]),
                PricePerTokenUsd = ReadDecimal(item["pricing"]?["prompt"]),
                FetchedAt = fetchedAt,
            });
        }

        return result;
    }

    public async Task<AudioGenerationResponse> GenerateAudioAsync(AudioGenerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Model);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);

        return request.Model.Output switch
        {
            AudioOutput.ChatAudio => await GenerateChatAudioAsync(request, cancellationToken),
            AudioOutput.Speech => await GenerateSpeechAsync(request, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unknown audio output type."),
        };
    }

    private async Task<AudioGenerationResponse> GenerateChatAudioAsync(AudioGenerationRequest request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var format = attempt == 0 ? "mp3" : "wav";
            var audio = new JsonObject { ["format"] = format };
            SetIfNotNull(audio, "voice", request.Voice);
            using var message = await CreateAuthorizedRequestAsync(HttpMethod.Post, "chat/completions", new JsonObject
            {
                ["model"] = request.Model.Id,
                ["messages"] = new JsonArray(new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = request.Prompt,
                }),
                ["modalities"] = new JsonArray("text", "audio"),
                ["audio"] = audio,
                ["stream"] = true,
            }, cancellationToken);
            message.Options.Set(ProviderResponseLimits.BodyKind, ProviderResponseBodyKind.Audio);
            var result = await OpenAudioResponseAsync(message, cancellationToken, isSse: true);
            if (result.Response is not null)
            {
                return result.Response;
            }

            if (attempt == 1 || result.ErrorStatus != HttpStatusCode.BadRequest || !IsAudioFormatOrVoiceError(result.ErrorMessage))
            {
                throw new ProviderException(
                    MapErrorKind((int)result.ErrorStatus!.Value, hasModerationReasons: false),
                    result.ErrorMessage,
                    statusCode: (int)result.ErrorStatus.Value);
            }
        }

        throw new InvalidOperationException("The OpenRouter audio retry did not complete.");
    }

    private async Task<AudioGenerationResponse> GenerateSpeechAsync(AudioGenerationRequest request, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["model"] = request.Model.Id,
            ["input"] = request.Prompt,
            ["response_format"] = "mp3",
        };
        SetIfNotNull(body, "voice", request.Voice);
        using var message = await CreateAuthorizedRequestAsync(HttpMethod.Post, "audio/speech", body, cancellationToken);
        message.Options.Set(ProviderResponseLimits.BodyKind, ProviderResponseBodyKind.Audio);
        var result = await OpenAudioResponseAsync(message, cancellationToken, isSse: false);
        if (result.Response is not null)
        {
            return result.Response;
        }

        throw new ProviderException(
            MapErrorKind((int)result.ErrorStatus!.Value, hasModerationReasons: false),
            result.ErrorMessage,
            statusCode: (int)result.ErrorStatus.Value);
    }

    private async Task<AudioResponseOpenResult> OpenAudioResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken, bool isSse)
    {
        var timeout = CreateTimeout(cancellationToken);
        HttpResponseMessage? response = null;
        var transferred = false;
        try
        {
            response = await CreateHttpClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var error = await BoundedHttpContentReader.ReadTextAsync(response.Content, ProviderResponseLimits.GetLimit(request), timeout.Token);
                return new AudioResponseOpenResult(null, response.StatusCode, ExtractAudioErrorMessage(error, response.StatusCode));
            }

            var audioBodyLimit = isSse
                ? ProviderResponseLimits.Audio + (ProviderResponseLimits.Audio / 3) + ProviderResponseLimits.Json
                : ProviderResponseLimits.Audio;
            if (response.Content.Headers.ContentLength is { } length && length > audioBodyLimit)
            {
                throw new ProviderException(
                    ErrorKind.Unknown,
                    "OpenRouter audio exceeded the configured size limit.",
                    innerException: new ProviderResponseTooLargeException(audioBodyLimit, length));
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            Stream content = isSse ? new SseAudioStream(source, timeout.Token) : new AudioFormatDetectingStream(source, timeout.Token);
            var owner = new AudioResponseOwner(response, timeout);
            response = null;
            transferred = true;
            return new AudioResponseOpenResult(new AudioGenerationResponse(content, isSse ? null : mediaType, owner), null, string.Empty);
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderException(ErrorKind.Network, "Could not reach OpenRouter.", innerException: exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException(ErrorKind.Timeout, "OpenRouter did not respond in time.", innerException: exception);
        }
        finally
        {
            response?.Dispose();
            if (!transferred)
            {
                timeout.Dispose();
            }
        }
    }

    private sealed record AudioResponseOpenResult(AudioGenerationResponse? Response, HttpStatusCode? ErrorStatus, string ErrorMessage);

    private sealed class AudioResponseOwner(HttpResponseMessage response, CancellationTokenSource timeout) : IDisposable
    {
        public void Dispose()
        {
            response.Dispose();
            timeout.Dispose();
        }
    }

    private static string ExtractAudioErrorMessage(string body, HttpStatusCode statusCode)
    {
        try
        {
            var parsed = JsonNode.Parse(body);
            return ReadString(parsed?["error"]?["message"]) ?? ReadString(parsed?["message"]) ?? body;
        }
        catch (System.Text.Json.JsonException)
        {
            return string.IsNullOrWhiteSpace(body) ? $"OpenRouter returned HTTP {(int)statusCode}." : body;
        }
    }

    private static bool IsAudioFormatOrVoiceError(string message) =>
        message.Contains("format", StringComparison.OrdinalIgnoreCase)
        || message.Contains("voice", StringComparison.OrdinalIgnoreCase);

    private static decimal? ReadPrice(JsonNode? node) =>
        decimal.TryParse(ReadString(node), NumberStyles.Float, CultureInfo.InvariantCulture, out var price) ? price : null;

    public async Task<ProviderJobHandle> SubmitAsync(VideoGenerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DurationSeconds);

        var body = new JsonObject
        {
            ["model"] = request.ModelId,
            ["prompt"] = request.Prompt,
            ["duration"] = request.DurationSeconds,
            ["generate_audio"] = request.GenerateAudio,
        };
        SetIfNotNull(body, "resolution", request.Resolution);
        SetIfNotNull(body, "aspect_ratio", request.AspectRatio);
        if (request.Seed is { } seed)
        {
            body["seed"] = seed;
        }

        if (request.FirstFrame is { } firstFrame)
        {
            if (firstFrame.Content.Length == 0
                || !firstFrame.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || firstFrame.MediaType.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException("The first frame must have image bytes and an image media type.", nameof(request));
            }

            var dataUrl = $"data:{firstFrame.MediaType};base64,{Convert.ToBase64String(firstFrame.Content)}";
            body["frame_images"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject { ["url"] = dataUrl },
                    ["frame_type"] = "first_frame",
                },
            };
        }

        using var requestMessage = await CreateAuthorizedRequestAsync(HttpMethod.Post, "videos", body, cancellationToken);
        var response = await SendJsonAsync(requestMessage, cancellationToken);
        var id = ReadString(response["id"]);
        var pollingUrl = ReadString(response["polling_url"]);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(pollingUrl))
        {
            throw new ProviderException(ErrorKind.EmptyResponse, "OpenRouter returned an incomplete video job handle.");
        }

        var statusUri = ValidateVideoAddress(pollingUrl, allowRelative: true);
        var contentUri = new Uri(Config.BaseAddress, $"videos/{Uri.EscapeDataString(id)}/content?index=0");
        return new ProviderJobHandle(Config.Id, id, statusUri.AbsoluteUri, contentUri.AbsoluteUri, string.Empty);
    }

    public async Task<VideoJobUpdate> PollAsync(ProviderJobHandle handle, CancellationToken cancellationToken)
    {
        var validated = ValidateVideoHandle(handle);
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, validated.StatusUrl, null, cancellationToken);
        var status = await SendVideoJsonAsync(request, cancellationToken);
        var state = ReadString(status["status"]);
        return state switch
        {
            "pending" => new VideoJobUpdate(JobState.Queued),
            "in_progress" => new VideoJobUpdate(JobState.Running),
            "completed" => new VideoJobUpdate(JobState.Succeeded, ReadCost(status)),
            "failed" => FailedVideoUpdate(status),
            "cancelled" or "expired" => new VideoJobUpdate(
                JobState.Failed, ErrorKind: ErrorKind.Unknown, ErrorMessage: $"Video generation {state}."),
            _ => new VideoJobUpdate(JobState.Queued),
        };
    }

    public async Task<ProviderContent> OpenContentAsync(ProviderJobHandle handle, CancellationToken cancellationToken)
    {
        var validated = ValidateVideoHandle(handle);
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, validated.ResponseUrl, null, cancellationToken);
        CancellationTokenSource? timeout = CreateTimeout(cancellationToken);
        HttpResponseMessage? response = null;
        try
        {
            response = await CreateHttpClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new ProviderException(
                    ProviderClientBase.MapErrorKind((int)response.StatusCode, hasModerationReasons: false),
                    $"OpenRouter returned HTTP {(int)response.StatusCode} while downloading video.",
                    statusCode: (int)response.StatusCode);
            }

            if (response.Content.Headers.ContentLength is { } length && length > ProviderResponseLimits.Video)
            {
                throw new ProviderException(
                    ErrorKind.Unknown,
                    "OpenRouter video exceeded the configured size limit.",
                    innerException: new ProviderResponseTooLargeException(ProviderResponseLimits.Video, length));
            }

            var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            var content = new ProviderContent(
                stream,
                string.IsNullOrWhiteSpace(mediaType) ? "video/mp4" : mediaType,
                response,
                timeout);
            response = null;
            timeout = null;
            return content;
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderException(ErrorKind.Network, "Could not download video from OpenRouter.", innerException: exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException(ErrorKind.Timeout, "OpenRouter video download timed out.", innerException: exception);
        }
        finally
        {
            request.Dispose();
            response?.Dispose();
            timeout?.Dispose();
        }
    }

    private ProviderJobHandle ValidateVideoHandle(ProviderJobHandle handle)
    {
        if (handle is null || !string.Equals(handle.ProviderId, Config.Id, StringComparison.Ordinal))
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter returned an invalid video job handle.");
        }

        return handle with
        {
            StatusUrl = ValidateVideoAddress(handle.StatusUrl, allowRelative: false).AbsoluteUri,
            ResponseUrl = ValidateVideoAddress(handle.ResponseUrl, allowRelative: false).AbsoluteUri,
        };
    }

    private Uri ValidateVideoAddress(string value, bool allowRelative)
    {
        Uri uri;
        if (allowRelative && Uri.TryCreate(value, UriKind.Relative, out var relative))
        {
            uri = new Uri(Config.BaseAddress, relative);
        }
        else if (!Uri.TryCreate(value, UriKind.Absolute, out uri!))
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter returned an invalid video URL.");
        }

        if (!value.StartsWith(uri.Scheme + "://", StringComparison.OrdinalIgnoreCase)
                && !allowRelative
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.IdnHost, Config.BaseAddress.IdnHost, StringComparison.OrdinalIgnoreCase)
            || uri.Port != Config.BaseAddress.Port
            || uri.UserInfo.Length != 0
            || uri.Fragment.Length != 0
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Any(char.IsControl))
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter returned an invalid video URL.");
        }

        return uri;
    }

    private async Task<JsonNode> SendVideoJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(cancellationToken);
        try
        {
            using (request)
            using (var response = await CreateHttpClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token))
            {
                var text = await BoundedHttpContentReader.ReadTextAsync(response.Content, ProviderResponseLimits.GetLimit(request), timeout.Token);
                JsonNode? root = null;
                try
                {
                    root = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
                }
                catch (System.Text.Json.JsonException)
                {
                    // Convert an invalid provider body into the same empty-response error used by other endpoints.
                }

                if (!response.IsSuccessStatusCode)
                {
                    var error = root?["error"];
                    var code = ReadInt(error?["code"]) ?? (int)response.StatusCode;
                    var message = ReadString(error?["message"]) ?? $"OpenRouter returned HTTP {(int)response.StatusCode}.";
                    throw new ProviderException(
                        ProviderClientBase.MapErrorKind(code, error?["metadata"]?["reasons"] is JsonArray),
                        message,
                        statusCode: (int)response.StatusCode);
                }

                return root ?? throw new ProviderException(ErrorKind.EmptyResponse, "OpenRouter returned an empty video status.");
            }
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderException(ErrorKind.Network, "Could not reach OpenRouter.", innerException: exception);
        }
        catch (ProviderResponseTooLargeException exception)
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter video status exceeded the size limit.", innerException: exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException(ErrorKind.Timeout, "OpenRouter did not respond in time.", innerException: exception);
        }
    }

    private static VideoJobUpdate FailedVideoUpdate(JsonNode status)
    {
        var error = status["error"];
        var code = ReadInt(error?["code"]);
        var message = ReadString(error?["message"]) ?? ReadString(error) ?? "OpenRouter video generation failed.";
        return new VideoJobUpdate(
            JobState.Failed,
            ErrorKind: code is { } value ? ProviderClientBase.MapErrorKind(value, error?["metadata"]?["reasons"] is JsonArray) : ErrorKind.Unknown,
            ErrorMessage: message);
    }

    private IReadOnlyDictionary<string, decimal> ReadVideoPrices(JsonNode? node, string modelId)
    {
        if (node is not JsonObject skus)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var prices = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (key, value) in skus)
        {
            if (value is null)
            {
                continue;
            }

            var text = ReadString(value);
            if (text is not null && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var price))
            {
                prices[key] = price;
            }
            else
            {
                _logger.LogDebug("Skipping unparseable video price SKU {Sku} for OpenRouter model {ModelId}.", key, modelId);
            }
        }

        return prices;
    }

    private static IReadOnlyList<int> ReadVideoIntegers(JsonNode? node) => node is JsonArray values
        ? values.Select(ReadInt).OfType<int>().Where(value => value > 0).Distinct().Order().ToArray()
        : [];

    private static IReadOnlyList<string> ReadVideoStrings(JsonNode? node) => node is JsonArray values
        ? values.Select(ReadString).OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray()
        : [];

    private static bool? ReadNullableBoolean(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var result) ? result : null;

    private async Task<IReadOnlyList<ImageModelPrice>> ReadPricesAsync(string modelId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = await CreateAuthorizedRequestAsync(
                HttpMethod.Get, "images/models/" + modelId + "/endpoints", null, cancellationToken);
            var root = await SendJsonAsync(request, cancellationToken);
            if (root["endpoints"] is not JsonArray endpoints)
            {
                return [];
            }

            var prices = new List<ImageModelPrice>();
            foreach (var endpoint in endpoints.OfType<JsonObject>())
            {
                if (endpoint["pricing"] is not JsonArray pricing)
                {
                    continue;
                }

                foreach (var entry in pricing.OfType<JsonObject>())
                {
                    var billable = ReadString(entry["billable"]);
                    var unit = ReadImagePriceUnit(ReadString(entry["unit"]));
                    if (string.IsNullOrWhiteSpace(billable)
                        || unit is null
                        || entry["cost_usd"] is not JsonValue costValue
                        || !costValue.TryGetValue<decimal>(out var cost)
                        || cost < 0)
                    {
                        continue;
                    }

                    string? variant = null;
                    if (entry.TryGetPropertyValue("variant", out var variantNode) && variantNode is not null)
                    {
                        variant = ReadString(variantNode);
                        if (variant is null)
                        {
                            continue;
                        }
                    }

                    prices.Add(new ImageModelPrice(billable, unit.Value, cost, variant));
                }
            }

            return prices;
        }
        catch (ProviderException)
        {
            return [];
        }
    }

    private static ImagePriceUnit? ReadImagePriceUnit(string? unit) => unit switch
    {
        "image" => ImagePriceUnit.Image,
        "megapixel" => ImagePriceUnit.Megapixel,
        "token" => ImagePriceUnit.Token,
        "request" => ImagePriceUnit.Request,
        _ => null,
    };

    public async Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = new JsonObject
        {
            ["model"] = request.ModelId,
            ["prompt"] = request.Prompt,
            ["n"] = 1,
        };
        if (request.Seed.HasValue)
        {
            body["seed"] = request.Seed.Value;
        }

        SetIfNotNull(body, GenerationParameterPlanner.AspectRatioName, request.AspectRatio);
        SetIfNotNull(body, GenerationParameterPlanner.ResolutionName, request.Resolution);
        SetIfNotNull(body, GenerationParameterPlanner.OutputFormatName, request.OutputFormat);
        SetIfNotNull(body, GenerationParameterPlanner.QualityName, request.Quality);
        if (request.ReferenceImageDataUrls is { Count: > 0 } references)
        {
            // Form assumed, not yet checked against the live API (task 018, owner decision).
            body[ReferenceImageLimit.ParameterName] = new JsonArray(references.Select(url => (JsonNode)JsonValue.Create(url)!).ToArray());
        }

        using var message = await CreateAuthorizedRequestAsync(HttpMethod.Post, "images", body, cancellationToken);
        message.Options.Set(ProviderResponseLimits.BodyKind, ProviderResponseBodyKind.GeneratedImageJson);
        var root = await SendJsonAsync(message, cancellationToken);
        var first = (root["data"] as JsonArray)?.FirstOrDefault();
        var base64 = first?["b64_json"]?.GetValue<string>();
        if (string.IsNullOrEmpty(base64))
        {
            throw new ProviderException(ErrorKind.EmptyResponse, "OpenRouter returned no image.");
        }

        var mediaType = first!["media_type"]?.GetValue<string>();
        try
        {
            return new ImageGenerationResult(
                DecodeImage(base64),
                string.IsNullOrWhiteSpace(mediaType) ? DefaultMediaType : mediaType,
                ReadCost(root));
        }
        catch (ProviderResponseTooLargeException ex)
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter response exceeded the configured size limit.", innerException: ex);
        }
    }

    private static byte[] DecodeImage(string base64)
        => BoundedBase64ImageDecoder.Decode(base64, ProviderResponseLimits.Image);

    /// <summary>Chat models of OpenRouter with their input modalities and context window. Entries without an id are skipped.</summary>
    public async Task<IReadOnlyList<ChatModel>> ListChatModelsAsync(CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "models", null, cancellationToken);
        var root = await SendJsonAsync(request, cancellationToken);
        var result = new List<ChatModel>();
        if (root["data"] is not JsonArray data)
        {
            return result;
        }

        foreach (var item in data)
        {
            var id = ReadString(item?["id"]);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var inputModalities = item!["architecture"]?["input_modalities"] as JsonArray;
            var supportedParameters = item["supported_parameters"] as JsonArray;
            result.Add(new ChatModel(
                id,
                ReadString(item["name"]) ?? id,
                inputModalities?.Any(m => ReadString(m) == "image") == true,
                ReadInt(item["context_length"]),
                SupportsTools: supportedParameters?.Any(p => ReadString(p) == "tools") == true,
                PromptPricePerMillion: PricePerMillion(item["pricing"]?["prompt"]),
                CompletionPricePerMillion: PricePerMillion(item["pricing"]?["completion"])));
        }

        return result;
    }

    public async Task<AssistantChatResult> CompleteChatAsync(AssistantChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var httpRequest = await CreateAuthorizedRequestAsync(
            HttpMethod.Post, "chat/completions", OpenAiChatWire.BuildBody(request), cancellationToken);
        var root = await SendJsonAsync(httpRequest, cancellationToken);
        var (text, toolCalls) = OpenAiChatWire.ReadReply(root, Config.DisplayName);
        return new AssistantChatResult(text, toolCalls, ReadCost(root), ReadPromptTokens(root));
    }

    public async Task<EmbeddingResult> EmbedAsync(string modelId, IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0)
        {
            throw new ArgumentException("At least one text is required.", nameof(texts));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var input = new JsonArray(texts.Select(text => (JsonNode?)JsonValue.Create(text)).ToArray());
        var body = new JsonObject
        {
            ["model"] = modelId,
            ["input"] = input,
        };
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, "embeddings", body, cancellationToken);
        var root = await SendJsonAsync(request, cancellationToken);
        var vectors = ReadEmbeddings(root, texts.Count);
        var model = root["model"] is JsonValue modelNode && modelNode.TryGetValue<string>(out var responseModel)
            && !string.IsNullOrWhiteSpace(responseModel)
            ? responseModel
            : modelId;
        return new EmbeddingResult(vectors, model, vectors[0].Length, ReadCost(root));
    }

    private static float[][] ReadEmbeddings(JsonNode root, int expectedCount)
    {
        if (root["data"] is not JsonArray data)
        {
            throw InvalidEmbeddingResponse("embedding response has no data array");
        }

        if (data.Count != expectedCount)
        {
            throw InvalidEmbeddingResponse($"embedding response has {data.Count} items, expected {expectedCount}");
        }

        var vectors = new float[expectedCount][];
        var dimensions = 0;
        for (var itemNumber = 0; itemNumber < data.Count; itemNumber++)
        {
            var item = data[itemNumber];
            if (item?["index"] is not JsonValue indexValue || !indexValue.TryGetValue<int>(out var index))
            {
                throw InvalidEmbeddingResponse($"embedding item {itemNumber} has no index");
            }

            if (index < 0 || index >= expectedCount || vectors[index] is not null)
            {
                throw InvalidEmbeddingResponse($"embedding item {itemNumber} has an invalid or duplicate index");
            }

            if (item["embedding"] is not JsonArray values)
            {
                throw InvalidEmbeddingResponse($"embedding item {itemNumber} has no vector");
            }

            if (values.Count == 0)
            {
                throw InvalidEmbeddingResponse($"embedding item {itemNumber} has an empty vector");
            }

            if (dimensions == 0)
            {
                dimensions = values.Count;
            }
            else if (values.Count != dimensions)
            {
                throw InvalidEmbeddingResponse($"embedding item {itemNumber} has {values.Count} values, expected {dimensions}");
            }

            var vector = new float[dimensions];
            for (var dimension = 0; dimension < values.Count; dimension++)
            {
                if (values[dimension] is not JsonValue valueNode
                    || !valueNode.TryGetValue<double>(out var value)
                    || !double.IsFinite(value)
                    || value > float.MaxValue
                    || value < -float.MaxValue)
                {
                    throw InvalidEmbeddingResponse($"embedding vector {index} contains a non-finite value");
                }

                vector[dimension] = (float)value;
                if (!float.IsFinite(vector[dimension]))
                {
                    throw InvalidEmbeddingResponse($"embedding vector {index} contains a non-finite value");
                }
            }

            vectors[index] = vector;
        }

        return vectors.Any(vector => vector is null)
            ? throw InvalidEmbeddingResponse("embedding response is missing one or more indexes")
            : vectors;
    }

    private static ProviderException InvalidEmbeddingResponse(string message) => new(ErrorKind.Unknown, message);

    private static int? ReadPromptTokens(JsonNode root) =>
        root["usage"]?["prompt_tokens"] is JsonValue tokens && tokens.TryGetValue<int>(out var count) ? count : null;

    private static decimal? ReadCost(JsonNode root)
    {
        if (root["usage"]?["cost"] is not JsonValue cost)
        {
            return null;
        }

        if (cost.TryGetValue<decimal>(out var number))
        {
            return number;
        }

        return cost.TryGetValue<string>(out var s)
            && decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>Maps the catalog's <c>supported_parameters</c>. Unknown kinds are skipped; a missing object gives an empty list.</summary>
    private static IReadOnlyList<ModelParameter> ReadParameters(JsonObject? parameters)
    {
        var result = new List<ModelParameter>();
        if (parameters is null)
        {
            return result;
        }

        foreach (var (name, definition) in parameters)
        {
            // A definition that is not an object (for example a bare flag) is not a parameter kind we know: skip it.
            if (definition is not JsonObject)
            {
                continue;
            }

            switch (ReadString(definition["type"]))
            {
                case "enum":
                    var values = (definition["values"] as JsonArray)?
                        .Select(ReadString)
                        .OfType<string>()
                        .ToList() ?? [];
                    result.Add(new ModelParameter(name, ParameterKind.Enum, values));
                    break;
                case "range":
                    result.Add(new ModelParameter(
                        name, ParameterKind.Range, [], ReadNumber(definition["min"]), ReadNumber(definition["max"])));
                    break;
                case "boolean":
                    result.Add(new ModelParameter(name, ParameterKind.Boolean, []));
                    break;
            }
        }

        return result;
    }

    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static double? ReadNumber(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

    private static void SetIfNotNull(JsonObject body, string name, string? value)
    {
        if (value is not null)
        {
            body[name] = value;
        }
    }

    /// <summary>OpenRouter gives the price per token as a decimal string; the app shows it per million tokens.</summary>
    private static decimal? PricePerMillion(JsonNode? perToken) =>
        decimal.TryParse(ReadString(perToken), NumberStyles.Float, CultureInfo.InvariantCulture, out var price)
            ? price * 1_000_000m
            : null;

    private static int? ReadInt(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static decimal? ReadDecimal(JsonNode? node) =>
        decimal.TryParse(ReadString(node), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : node is JsonValue value && value.TryGetValue<decimal>(out parsed) ? parsed : null;

    private static int ReadMaxImages(JsonObject? parameters)
    {
        if (parameters?["n"]?["max"] is JsonValue max && max.TryGetValue<int>(out var value) && value > 0)
        {
            return value;
        }

        return 1;
    }
}
