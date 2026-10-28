using System.Text.Json.Nodes;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;

namespace ImageGen.Core.OpenAiCompatible;

/// <summary>Request and reply of <c>chat/completions</c> in the OpenAI format (OpenRouter and OpenAI-compatible APIs).</summary>
internal static class OpenAiChatWire
{
    public static JsonObject BuildBody(AssistantChatRequest request)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            messages.Add(BuildMessage(message));
        }

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
        };
        if (request.Tools.Count > 0)
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = JsonNode.Parse(tool.ParametersJsonSchema),
                    },
                });
            }

            body["tools"] = tools;
            body["tool_choice"] = "auto";
        }

        return body;
    }

    /// <summary>Reads the first choice. Throws <see cref="ProviderException"/> when there is none.</summary>
    public static (string? Text, List<AssistantToolCall> ToolCalls) ReadReply(JsonNode root, string providerName)
    {
        var choiceMessage = (root["choices"] as JsonArray)?.FirstOrDefault()?["message"];
        if (choiceMessage is null)
        {
            throw new ProviderException(ErrorKind.EmptyResponse, $"{providerName} returned no choices.");
        }

        var text = choiceMessage["content"] is JsonValue content && content.TryGetValue<string>(out var value) ? value : null;
        var toolCalls = new List<AssistantToolCall>();
        if (choiceMessage["tool_calls"] is JsonArray calls)
        {
            foreach (var call in calls)
            {
                var name = call?["function"]?["name"]?.GetValue<string>();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var arguments = call!["function"]!["arguments"];
                var argumentsJson = arguments is JsonValue argumentsValue && argumentsValue.TryGetValue<string>(out var s)
                    ? s
                    : arguments?.ToJsonString() ?? "{}";
                toolCalls.Add(new AssistantToolCall(name, argumentsJson));
            }
        }

        return (text, toolCalls);
    }

    private static JsonObject BuildMessage(AssistantChatMessage message)
    {
        if (message.ImageDataUrls.Count == 0)
        {
            return new JsonObject { ["role"] = message.Role, ["content"] = message.Text };
        }

        var parts = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = message.Text } };
        foreach (var url in message.ImageDataUrls)
        {
            parts.Add(new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject { ["url"] = url },
            });
        }

        return new JsonObject { ["role"] = message.Role, ["content"] = parts };
    }
}
