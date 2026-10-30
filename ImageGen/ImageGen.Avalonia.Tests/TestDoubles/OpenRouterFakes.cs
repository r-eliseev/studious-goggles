using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace ImageGen.Avalonia.Tests.TestDoubles;

/// <summary>
/// OpenRouter as the app talks to it, answered by path: key check, chat completions (with a tool call), image generation
/// and the image model catalog. No network.
/// </summary>
public static class OpenRouterFakes
{
    /// <summary>A 1×1 PNG, so that the card can show a real image.</summary>
    public const string OnePixelPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    /// <summary>The image models of the test catalog: the default model (with seed) and one without seed.</summary>
    public const string CatalogJson = """
        {"data":[
          {"id":"black-forest-labs/flux.2-klein-4b","name":"FLUX.2 Klein 4B","supported_parameters":{"seed":true}},
          {"id":"vendor/beta","name":"Beta Pro","supported_parameters":{}}
        ]}
        """;

    /// <param name="chatStatus">HTTP status of chat completions; anything but 200 is an error reply.</param>
    /// <param name="toolArgumentsJson">Arguments of the generate_images call the assistant makes.</param>
    /// <param name="imagesFiltered">When true, image generation answers 403 with a moderation reason, so the card is filtered.</param>
    /// <param name="failedChatReplies">How many chat replies use <paramref name="chatStatus"/> before the rest answer normally.</param>
    public static StubHttpMessageHandler Create(
        HttpStatusCode chatStatus = HttpStatusCode.OK,
        string toolArgumentsJson = """{"prompt":"a red fox"}""",
        bool imagesFiltered = false,
        int failedChatReplies = int.MaxValue)
    {
        var chatReplies = 0;
        return new(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/key", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, "{}");
            }

            if (path.EndsWith("/endpoints", StringComparison.Ordinal))
            {
                return path.Contains("/black-forest-labs/flux.2-klein-4b/endpoints", StringComparison.Ordinal)
                    ? Json(HttpStatusCode.OK, """{"endpoints":[{"pricing":[{"billable":"output_image","unit":"megapixel","cost_usd":0.014}]}]}""")
                    : Json(HttpStatusCode.OK, """{"endpoints":[]}""");
            }

            if (path.EndsWith("/images/models", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, CatalogJson);
            }

            if (path.EndsWith("/images", StringComparison.Ordinal))
            {
                return imagesFiltered
                    ? Json(HttpStatusCode.Forbidden, """{"error":{"code":403,"message":"filtered","metadata":{"reasons":["content_policy"]}}}""")
                    : Json(HttpStatusCode.OK, $$"""{"data":[{"b64_json":"{{OnePixelPngBase64}}","media_type":"image/png"}]}""");
            }

            if (path.EndsWith("/chat/completions", StringComparison.Ordinal))
            {
                var failing = chatStatus != HttpStatusCode.OK && Interlocked.Increment(ref chatReplies) <= failedChatReplies;
                return failing
                    ? Json(chatStatus, """{"error":{"code":429,"message":"slow down"}}""")
                    : Json(HttpStatusCode.OK, ToolCallReply(toolArgumentsJson));
            }

            return Json(HttpStatusCode.NotFound, "{}");
        });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string ToolCallReply(string arguments)
    {
        var call = new JsonObject
        {
            ["id"] = "call-1",
            ["type"] = "function",
            ["function"] = new JsonObject { ["name"] = "generate_images", ["arguments"] = arguments },
        };
        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = null,
            ["tool_calls"] = new JsonArray(call),
        };
        var root = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject { ["finish_reason"] = "tool_calls", ["message"] = message }),
        };
        return root.ToJsonString();
    }
}
