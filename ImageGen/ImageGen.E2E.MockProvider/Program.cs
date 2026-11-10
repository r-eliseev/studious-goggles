using System.Security.Cryptography;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/v1/models", (HttpRequest request) =>
{
    if (!HasValidKey(request))
    {
        return Results.Unauthorized();
    }

    return Results.Json(new
    {
        data = new[]
        {
            new { id = "e2e/assistant", @object = "model", owned_by = "e2e-mock" },
            new { id = "e2e/image", @object = "model", owned_by = "e2e-mock" },
        },
    });
});

app.MapPost("/v1/chat/completions", async (HttpRequest request) =>
{
    if (!HasValidKey(request))
    {
        return Results.Unauthorized();
    }

    using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
    var root = document.RootElement;
    if (root.GetProperty("model").GetString() != "e2e/assistant")
    {
        return Results.BadRequest(new { error = new { message = "Unknown E2E assistant model." } });
    }

    var prompt = root.GetProperty("messages").EnumerateArray()
        .Where(message => message.TryGetProperty("role", out var role) && role.GetString() == "user")
        .Select(message => message.GetProperty("content").GetString())
        .LastOrDefault(value => !string.IsNullOrWhiteSpace(value));
    if (prompt is null)
    {
        return Results.BadRequest(new { error = new { message = "A user prompt is required." } });
    }

    return Results.Json(new
    {
        id = "chatcmpl-e2e-mock",
        @object = "chat.completion",
        choices = new[]
        {
            new
            {
                index = 0,
                message = new
                {
                    role = "assistant",
                    content = (string?)null,
                    tool_calls = new[]
                    {
                        new
                        {
                            id = "call-e2e-image",
                            type = "function",
                            function = new
                            {
                                name = "generate_images",
                                arguments = JsonSerializer.Serialize(new { prompt }),
                            },
                        },
                    },
                },
                finish_reason = "tool_calls",
            },
        },
    });
});

app.MapPost("/v1/images/generations", async (HttpRequest request) =>
{
    if (!HasValidKey(request))
    {
        return Results.Unauthorized();
    }

    using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
    if (document.RootElement.GetProperty("model").GetString() != "e2e/image")
    {
        return Results.BadRequest(new { error = new { message = "Unknown E2E image model." } });
    }

    var image = Convert.ToBase64String(ReadImage());
    return Results.Json(new
    {
        data = new[] { new { b64_json = image } },
    });
});

app.Run();

static bool HasValidKey(HttpRequest request) =>
    string.Equals(request.Headers.Authorization.ToString(), "Bearer e2e-mock-key", StringComparison.Ordinal);

static byte[] ReadImage()
{
    using var stream = typeof(Program).Assembly.GetManifestResourceStream("ImageGen.E2E.MockProvider.Resources.e2e-image.png")
        ?? throw new InvalidOperationException("The E2E mock image resource is missing.");
    using var memory = new MemoryStream();
    stream.CopyTo(memory);
    return memory.ToArray();
}

public partial class Program;
