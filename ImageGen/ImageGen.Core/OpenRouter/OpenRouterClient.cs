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
    : ProviderClientBase(config, httpClientFactory, secretStore, timeProvider), IModelProvider, IAssistantChatClient
{
    protected override void Authenticate(HttpRequestMessage request, string apiKey) => AuthenticateBearer(request, apiKey);

    public override Task<ApiKeyStatus> CheckKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        return ProbeKeyAsync(HttpMethod.Get, "key", apiKey, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageModel>> ListImageModelsAsync(CancellationToken cancellationToken)
    {
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

        return result;
    }

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

        using var message = await CreateAuthorizedRequestAsync(HttpMethod.Post, "images", body, cancellationToken);
        var root = await SendJsonAsync(message, cancellationToken);
        var first = (root["data"] as JsonArray)?.FirstOrDefault();
        var base64 = first?["b64_json"]?.GetValue<string>();
        if (string.IsNullOrEmpty(base64))
        {
            throw new ProviderException(ErrorKind.EmptyResponse, "OpenRouter returned no image.");
        }

        var mediaType = first!["media_type"]?.GetValue<string>();
        return new ImageGenerationResult(
            Convert.FromBase64String(base64),
            string.IsNullOrWhiteSpace(mediaType) ? DefaultMediaType : mediaType,
            ReadCost(root));
    }

    public async Task<AssistantChatResult> CompleteChatAsync(AssistantChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var httpRequest = await CreateAuthorizedRequestAsync(
            HttpMethod.Post, "chat/completions", OpenAiChatWire.BuildBody(request), cancellationToken);
        var root = await SendJsonAsync(httpRequest, cancellationToken);
        var (text, toolCalls) = OpenAiChatWire.ReadReply(root, Config.DisplayName);
        return new AssistantChatResult(text, toolCalls, ReadCost(root));
    }

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

    private static int ReadMaxImages(JsonObject? parameters)
    {
        if (parameters?["n"]?["max"] is JsonValue max && max.TryGetValue<int>(out var value) && value > 0)
        {
            return value;
        }

        return 1;
    }
}
