using System.Globalization;
using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.OpenAiCompatible;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;

namespace ImageGen.Core.OpenRouter;

/// <summary>OpenRouter: images through <c>images</c> and <c>images/models</c>, chat through <c>chat/completions</c>.</summary>
public sealed class OpenRouterClient(
    ProviderConfig config,
    IHttpClientFactory httpClientFactory,
    ISecretStore secretStore,
    TimeProvider timeProvider)
    : ProviderClientBase(config, httpClientFactory, secretStore, timeProvider), IModelProvider, IAssistantChatClient, ISynchronousImageTransport
{
    private const int PriceRequestParallelism = 4;

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

    private static int ReadMaxImages(JsonObject? parameters)
    {
        if (parameters?["n"]?["max"] is JsonValue max && max.TryGetValue<int>(out var value) && value > 0)
        {
            return value;
        }

        return 1;
    }
}
