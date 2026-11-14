using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace ImageGen.Acceptance.Tests.Support;

public sealed class FakeOpenRouter
{
    public const string Host = "openrouter.ai";

    private readonly FakeNetwork _network;
    private readonly ConcurrentQueue<AssistantReply> _assistantReplies = new();
    private readonly ConcurrentDictionary<string, GenerationReply> _generationReplies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, float[]> _embeddingVectors = new(StringComparer.Ordinal);
    private IReadOnlyList<ImageModel> _imageModels = [];
    private IReadOnlyList<EmbeddingModelInfo> _embeddingModels = [new("baai/bge-m3", "BGE M3")];
    private IReadOnlyList<AssistantModel> _assistantModels = [new("anthropic/claude-haiku-5.5", "Claude Haiku 5.5")];
    private HttpStatusCode _keyStatusCode = HttpStatusCode.OK;
    private bool _keyNetworkError;

    public FakeOpenRouter(FakeNetwork network)
    {
        _network = network;
        network.AddRoute(HttpMethod.Get, Host, "/api/v1/key", (_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_keyNetworkError) throw new HttpRequestException("Fake OpenRouter connection failure.");
            return Task.FromResult(JsonResponse(_keyStatusCode, "{}"));
        });
        network.AddRoute(HttpMethod.Get, Host, "/api/v1/embeddings/models", (_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, EmbeddingModelsJson())));
        network.AddRoute(HttpMethod.Post, Host, "/api/v1/embeddings", HandleEmbeddingsAsync);
        network.AddRoute(HttpMethod.Get, Host, "/api/v1/models",
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, AssistantModelsJson())));
        network.AddResponse(HttpMethod.Get, Host, "/api/v1/videos/models", HttpStatusCode.OK, "{\"data\":[]}");
        network.AddRoute(HttpMethod.Get, Host, "/api/v1/images/models", (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ImageModelsJson())));
        network.AddRoute(HttpMethod.Get, Host, "/api/v1/images/models/*/endpoints", (request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var modelId = Uri.UnescapeDataString(path["/api/v1/images/models/".Length..^"/endpoints".Length]);
            var model = _imageModels.FirstOrDefault(candidate => candidate.Id == modelId);
            var endpoints = new JsonArray();
            if (model is { PricePerMegapixel: not null } or { PricePerImageUsd: not null })
            {
                var pricing = new JsonArray();
                if (model.PricePerMegapixel is { } megapixelPrice)
                {
                    pricing.Add(new JsonObject
                    {
                        ["billable"] = "output_image",
                        ["unit"] = "megapixel",
                        ["cost_usd"] = megapixelPrice,
                    });
                }

                if (model.PricePerImageUsd is { } imagePrice)
                {
                    pricing.Add(new JsonObject
                    {
                        ["billable"] = "output_image",
                        ["unit"] = "image",
                        ["cost_usd"] = imagePrice,
                    });
                }

                pricing.Add(new JsonObject
                {
                    ["billable"] = "input_image",
                    ["unit"] = "megapixel",
                    ["cost_usd"] = 9.99m,
                });

                endpoints.Add(new JsonObject { ["pricing"] = pricing });
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, new JsonObject { ["endpoints"] = endpoints }.ToJsonString()));
        });
        network.AddRoute(HttpMethod.Post, Host, "/api/v1/chat/completions", HandleChatAsync);
        network.AddRoute(HttpMethod.Post, Host, "/api/v1/images", HandleImageAsync);
    }

    public void SetImageModels(IEnumerable<ImageModel> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        _imageModels = models.ToArray();
    }

    public void SetAssistantModels(IEnumerable<AssistantModel> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        _assistantModels = models.ToArray();
    }

    public void SetEmbeddingModels(IEnumerable<EmbeddingModelInfo> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        _embeddingModels = models.ToArray();
    }

    public void SetEmbeddingVectors(IEnumerable<KeyValuePair<string, float[]>> vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        _embeddingVectors.Clear();
        foreach (var pair in vectors) _embeddingVectors[pair.Key] = pair.Value.ToArray();
    }

    public void SetKeyResponse(HttpStatusCode statusCode)
    {
        _keyStatusCode = statusCode;
        _keyNetworkError = false;
    }

    public void SetKeyNetworkError() => _keyNetworkError = true;

    public void QueueAssistantText(string text) => _assistantReplies.Enqueue(new AssistantReply(text));

    public void QueueGenerateImages(string prompt) => _assistantReplies.Enqueue(new AssistantReply(ToolName: "generate_images", ToolArguments: new JsonObject { ["prompt"] = prompt }));

    public void QueueGenerateVideo(string prompt) => _assistantReplies.Enqueue(new AssistantReply(ToolName: "generate_video", ToolArguments: new JsonObject { ["prompt"] = prompt }));

    public void QueueAssistantError(HttpStatusCode statusCode, string body) => _assistantReplies.Enqueue(new AssistantReply(ErrorStatus: statusCode, ErrorBody: body));

    public void SetImage(string modelId, int width = 64, int height = 64, decimal cost = 0.01m) =>
        _generationReplies[modelId] = GenerationReply.Image(width, height, cost);

    public void SetImageWithoutCost(string modelId, int width = 64, int height = 64) =>
        _generationReplies[modelId] = GenerationReply.Image(width, height, cost: null);

    public void SetGenerationError(string modelId, HttpStatusCode statusCode, string body) =>
        _generationReplies[modelId] = GenerationReply.Error(statusCode, body);

    public void SetFiltered(string modelId) =>
        _generationReplies[modelId] = GenerationReply.Filtered();

    public FakeNetwork.HeldResponse HoldGeneration(string modelId)
    {
        var held = new FakeNetwork.HeldResponse();
        _generationReplies[modelId] = GenerationReply.Pending(held);
        return held;
    }

    public void CompleteHeldGeneration(string modelId, int width = 64, int height = 64, decimal cost = 0.01m)
    {
        if (!_generationReplies.TryGetValue(modelId, out var reply) || reply.Held is not { } held)
        {
            throw new InvalidOperationException($"No held generation exists for model '{modelId}'.");
        }

        var b64 = Convert.ToBase64String(CreatePng(width, height));
        var responseBody = new JsonObject
        {
            ["data"] = new JsonArray(new JsonObject { ["b64_json"] = b64, ["media_type"] = "image/png" }),
            ["usage"] = new JsonObject { ["cost"] = cost },
        }.ToJsonString();
        held.Release(HttpStatusCode.OK, responseBody);
    }

    private Task<HttpResponseMessage> HandleChatAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_assistantReplies.TryDequeue(out var reply))
        {
            throw new InvalidOperationException("No assistant response was queued for the OpenRouter fake.");
        }

        if (reply.ErrorStatus is { } status)
        {
            return Task.FromResult(JsonResponse(status, reply.ErrorBody ?? "{}"));
        }

        var message = new JsonObject { ["role"] = "assistant", ["content"] = reply.Text };
        string finishReason = "stop";
        if (reply.ToolName is { } toolName)
        {
            finishReason = "tool_calls";
            var call = new JsonObject
            {
                ["id"] = "call-acceptance-1",
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = toolName,
                    ["arguments"] = reply.ToolArguments?.ToJsonString() ?? "{}",
                },
            };
            message["content"] = null;
            message["tool_calls"] = new JsonArray(call);
        }

        var root = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["finish_reason"] = finishReason,
                ["message"] = message,
            }),
        };
        return Task.FromResult(JsonResponse(HttpStatusCode.OK, root.ToJsonString()));
    }

    private async Task<HttpResponseMessage> HandleEmbeddingsAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var root = JsonNode.Parse(body) ?? throw new InvalidOperationException("The embedding request has no JSON body.");
        var modelId = root["model"]?.GetValue<string>() ?? "baai/bge-m3";
        var inputs = root["input"]?.AsArray() ?? throw new InvalidOperationException("The embedding request has no input array.");
        var data = new JsonArray();
        for (var index = 0; index < inputs.Count; index++)
        {
            var text = inputs[index]?.GetValue<string>() ?? string.Empty;
            var vector = _embeddingVectors.TryGetValue(text, out var configured) ? configured : [1f, 0f, 0f];
            data.Add(new JsonObject
            {
                ["index"] = index,
                ["embedding"] = new JsonArray(vector.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
            });
        }

        return JsonResponse(HttpStatusCode.OK, new JsonObject
        {
            ["model"] = modelId,
            ["data"] = data,
            ["usage"] = new JsonObject { ["prompt_tokens"] = inputs.Count },
        }.ToJsonString());
    }

    private async Task<HttpResponseMessage> HandleImageAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var modelId = JsonNode.Parse(body)?["model"]?.GetValue<string>()
            ?? throw new InvalidOperationException("The OpenRouter image request has no model id.");
        if (!_generationReplies.TryGetValue(modelId, out var reply)) reply = GenerationReply.Image(64, 64, 0.01m);
        if (reply.Held is { } held) return await held.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (reply.IsFiltered)
        {
            return JsonResponse(HttpStatusCode.Forbidden, "{\"error\":{\"code\":403,\"message\":\"filtered\",\"metadata\":{\"reasons\":[\"content_policy\"]}}}");
        }
        if (reply.ErrorStatus is { } errorStatus) return JsonResponse(errorStatus, reply.ErrorBody ?? "{}");

        var png = CreatePng(reply.Width, reply.Height);
        var b64 = Convert.ToBase64String(png);
        var responseBody = new JsonObject
        {
            ["data"] = new JsonArray(new JsonObject { ["b64_json"] = b64, ["media_type"] = "image/png" }),
            ["usage"] = reply.Cost is { } cost ? new JsonObject { ["cost"] = cost } : null,
        }.ToJsonString();
        return JsonResponse(HttpStatusCode.OK, responseBody);
    }

    private string ImageModelsJson()
    {
        var models = new JsonArray();
        foreach (var model in _imageModels)
        {
            var supportedParameters = new JsonObject();
            if (model.SupportsSeed)
            {
                supportedParameters["seed"] = true;
            }

            if (model.MaxInputReferences is { } maxReferences)
            {
                supportedParameters["input_references"] = new JsonObject
                {
                    ["type"] = "range",
                    ["min"] = 1,
                    ["max"] = maxReferences,
                };
            }

            AddEnumParameter(supportedParameters, "aspect_ratio", model.AspectRatios);
            AddEnumParameter(supportedParameters, "resolution", model.Resolutions);
            AddEnumParameter(supportedParameters, "output_format", model.Formats);
            AddEnumParameter(supportedParameters, "quality", model.Qualities);

            models.Add(new JsonObject
            {
                ["id"] = model.Id,
                ["name"] = model.Name,
                ["supported_parameters"] = supportedParameters,
            });
        }

        return new JsonObject { ["data"] = models }.ToJsonString();
    }

    private string EmbeddingModelsJson() => new JsonObject
    {
        ["data"] = new JsonArray(_embeddingModels.Select(model => (JsonNode?)new JsonObject
        {
            ["id"] = model.Id,
            ["name"] = model.Name,
            ["context_length"] = 8192,
            ["pricing"] = new JsonObject { ["prompt"] = 0.0000001m },
        }).ToArray()),
    }.ToJsonString();

    private string AssistantModelsJson()
    {
        var models = new JsonArray();
        foreach (var model in _assistantModels)
        {
            models.Add(new JsonObject
            {
                ["id"] = model.Id,
                ["name"] = model.Name,
                ["supported_parameters"] = new JsonArray("tools"),
            });
        }

        return new JsonObject { ["data"] = models }.ToJsonString();
    }

    private static void AddEnumParameter(JsonObject parameters, string name, IReadOnlyList<string>? values)
    {
        if (values is not { Count: > 0 }) return;
        parameters[name] = new JsonObject
        {
            ["type"] = "enum",
            ["values"] = new JsonArray(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static byte[] CreatePng(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var raw = new byte[height * (1 + width * 4)];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + width * 4);
            for (var x = 0; x < width; x++)
            {
                var pixel = row + 1 + x * 4;
                raw[pixel] = 64;
                raw[pixel + 1] = 128;
                raw[pixel + 2] = 192;
                raw[pixel + 3] = byte.MaxValue;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream output, string name, byte[] data)
    {
        var type = Encoding.ASCII.GetBytes(name);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);
        var crc = Crc32(type, data);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc);
        output.Write(checksum);
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type) crc = UpdateCrc(crc, value);
        foreach (var value in data) crc = UpdateCrc(crc, value);
        return ~crc;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return crc;
    }

    private sealed record AssistantReply(
        string? Text = null,
        string? ToolName = null,
        JsonObject? ToolArguments = null,
        HttpStatusCode? ErrorStatus = null,
        string? ErrorBody = null);

    private sealed record GenerationReply(
        int Width,
        int Height,
        decimal? Cost,
        HttpStatusCode? ErrorStatus = null,
        string? ErrorBody = null,
        bool IsFiltered = false,
        FakeNetwork.HeldResponse? Held = null)
    {
        public static GenerationReply Image(int width, int height, decimal cost) => new(width, height, cost);

        public static GenerationReply Image(int width, int height, decimal? cost) => new(width, height, cost);

        public static GenerationReply Error(HttpStatusCode statusCode, string body) => new(0, 0, 0, statusCode, body);

        public static GenerationReply Filtered() => new(0, 0, 0, IsFiltered: true);

        public static GenerationReply Pending(FakeNetwork.HeldResponse response) => new(0, 0, 0, Held: response);
    }

    public sealed record ImageModel(
        string Id,
        string Name,
        bool SupportsSeed,
        decimal? PricePerMegapixel,
        decimal? PricePerImageUsd = null,
        IReadOnlyList<string>? AspectRatios = null,
        IReadOnlyList<string>? Resolutions = null,
        IReadOnlyList<string>? Formats = null,
        IReadOnlyList<string>? Qualities = null,
        int? MaxInputReferences = null);

    public sealed record AssistantModel(string Id, string Name);

    public sealed record EmbeddingModelInfo(string Id, string Name);
}
