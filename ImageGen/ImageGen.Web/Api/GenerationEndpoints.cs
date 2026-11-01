using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.AspNetCore.Mvc;

namespace ImageGen.Web.Api;

public static class GenerationEndpoints
{
    private const long MaxMessageRequestBytes = (AttachmentService.MaxAttachmentBytes * AttachmentService.MaxAttachmentsPerMessage) + (1024 * 1024);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/sessions/{sessionId:guid}/messages", SendMessageAsync)
            .AddEndpointFilter<Auth.AuthEndpoints.AntiforgeryFilter>()
            .WithMetadata(new RequestSizeLimitAttribute(MaxMessageRequestBytes));

        endpoints.MapPost("/api/generations/{generationId:guid}/repeat", async (
            Guid generationId,
            GenerationService generations) => await StartAsync(() => generations.RepeatAsync(generationId, CancellationToken.None)))
            .AddEndpointFilter<Auth.AuthEndpoints.AntiforgeryFilter>();

        endpoints.MapPost("/api/generations/{generationId:guid}/new-seed", async (
            Guid generationId,
            GenerationService generations) => await StartAsync(() => generations.NewSeedAsync(generationId, CancellationToken.None)))
            .AddEndpointFilter<Auth.AuthEndpoints.AntiforgeryFilter>();

        endpoints.MapPost("/api/generations/{generationId:guid}/edit", async (
            Guid generationId,
            EditGenerationRequest request,
            GenerationService generations,
            CancellationToken cancellationToken) => await StartAsync(() => generations.EditAsync(generationId, request.Prompt, CancellationToken.None)))
            .AddEndpointFilter<Auth.AuthEndpoints.AntiforgeryFilter>();

        endpoints.MapPost("/api/generations/{generationId:guid}/other-model", async (
            Guid generationId,
            OtherModelRequest request,
            GenerationService generations,
            CancellationToken cancellationToken) => await StartAsync(() => generations.RunOnModelAsync(generationId, request.Model, CancellationToken.None)))
            .AddEndpointFilter<Auth.AuthEndpoints.AntiforgeryFilter>();

        endpoints.MapGet("/api/generations/{generationId:guid}/image", async (
            Guid generationId,
            IRunRepository runs,
            IImageFileStore files,
            IAppPaths paths,
            CancellationToken cancellationToken) =>
        {
            var generation = await runs.GetGenerationAsync(generationId, cancellationToken);
            if (generation?.ImagePath is not { Length: > 0 } relativePath)
            {
                return Results.NotFound(new { error = "not_found" });
            }

            var fullPath = Path.GetFullPath(files.GetFullPath(relativePath));
            var dataDirectory = Path.GetFullPath(paths.DataDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(dataDirectory, comparison) || !File.Exists(fullPath))
            {
                return Results.NotFound(new { error = "not_found" });
            }

            var mediaType = generation.MediaType is "image/png" or "image/jpeg" or "image/webp"
                ? generation.MediaType
                : "application/octet-stream";
            return Results.File(fullPath, mediaType);
        });
    }

    private static async Task<IResult> SendMessageAsync(
        Guid sessionId,
        HttpRequest request,
        SessionService sessions,
        AttachmentService attachments,
        AssistantService assistant,
        CancellationToken requestCancellation)
    {
        if (await sessions.GetAsync(sessionId, requestCancellation) is null)
        {
            return Results.NotFound(new { error = "not_found" });
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        var form = await request.ReadFormAsync(requestCancellation);
        if (form.Files.Count > AttachmentService.MaxAttachmentsPerMessage)
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        if (form.Files.Any(file => file.Length is 0 or > AttachmentService.MaxAttachmentBytes
            || file.ContentType is not ("image/png" or "image/jpeg" or "image/webp")))
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        var text = form["text"].ToString();
        if (form.Files.Count == 0 && string.IsNullOrWhiteSpace(text))
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        var attachmentIds = new List<Guid>(form.Files.Count);
        foreach (var file in form.Files)
        {
            if (file.Length > AttachmentService.MaxAttachmentBytes)
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }

            await using var stream = new MemoryStream((int)file.Length);
            await file.CopyToAsync(stream, requestCancellation);
            var attachment = await attachments.AddImageAsync(sessionId, stream.ToArray(), file.ContentType, requestCancellation);
            attachmentIds.Add(attachment.Id);
        }

        try
        {
            var result = await assistant.SendUserMessageAsync(sessionId, text, attachmentIds, CancellationToken.None);
            return Results.Ok(new SendMessageResponse(
                WebApiEndpoints.MessageResponse.From(result.UserMessage),
                WebApiEndpoints.MessageResponse.From(result.AssistantMessage),
                result.Run is null ? null : WebApiEndpoints.RunResponse.From(result.Run)));
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound(new { error = "not_found" });
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }
        catch (ProviderException exception)
        {
            return Results.Json(new { error = exception.Kind.ToString() }, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> StartAsync(Func<Task<ImageGen.Core.Domain.Generations.Run>> start)
    {
        try
        {
            var run = await start();
            return Results.Accepted($"/api/sessions/{run.SessionId:D}/timeline", WebApiEndpoints.RunResponse.From(run));
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound(new { error = "not_found" });
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }
        catch (ProviderException exception)
        {
            return Results.Json(new { error = exception.Kind.ToString() }, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    public sealed record EditGenerationRequest(string Prompt);

    public sealed record OtherModelRequest(ImageGen.Core.Domain.Models.ModelRef Model);

    public sealed record SendMessageResponse(
        WebApiEndpoints.MessageResponse User,
        WebApiEndpoints.MessageResponse Assistant,
        WebApiEndpoints.RunResponse? Run);
}
