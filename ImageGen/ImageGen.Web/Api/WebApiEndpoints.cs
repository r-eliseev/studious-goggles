using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Web.Auth;

namespace ImageGen.Web.Api;

public static class WebApiEndpoints
{
    public static IEndpointRouteBuilder MapWebApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        MapSessions(endpoints.MapGroup("/api/sessions"));
        MapModelsAndProviders(endpoints.MapGroup("/api"));
        GenerationEndpoints.Map(endpoints);
        return endpoints;
    }

    private static void MapSessions(RouteGroupBuilder group)
    {
        group.MapGet("", async (SessionService sessions, CancellationToken cancellationToken) =>
        {
            var values = await sessions.ListAsync(cancellationToken);
            return Results.Ok(values.Select(SessionResponse.From));
        });

        group.MapPost("", async (SessionService sessions, CancellationToken cancellationToken) =>
        {
            var session = await sessions.CreateAsync(cancellationToken);
            return Results.Created($"/api/sessions/{session.Id:D}", SessionResponse.From(session));
        }).AddEndpointFilter<AuthEndpoints.AntiforgeryFilter>();

        group.MapGet("/{sessionId:guid}/timeline", async (Guid sessionId, SessionService sessions, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(TimelineResponse.From(await sessions.GetTimelineAsync(sessionId, cancellationToken)));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { error = "not_found" });
            }
        });

        group.MapPatch("/{sessionId:guid}", async (Guid sessionId, RenameRequest request, SessionService sessions, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(SessionResponse.From(await sessions.RenameAsync(sessionId, request.Title, cancellationToken)));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { error = "not_found" });
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }
        }).AddEndpointFilter<AuthEndpoints.AntiforgeryFilter>();

        group.MapDelete("/{sessionId:guid}", async (Guid sessionId, SessionService sessions, CancellationToken cancellationToken) =>
        {
            if (await sessions.GetAsync(sessionId, cancellationToken) is null)
            {
                return Results.NotFound(new { error = "not_found" });
            }

            await sessions.DeleteAsync(sessionId, cancellationToken);
            return Results.NoContent();
        }).AddEndpointFilter<AuthEndpoints.AntiforgeryFilter>();

        group.MapPut("/{sessionId:guid}/settings", async (Guid sessionId, SessionSettings settings, SessionService sessions, CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await sessions.UpdateSettingsAsync(sessionId, settings, cancellationToken);
                session = await sessions.UpdateGenerationParametersAsync(sessionId, settings.Parameters, cancellationToken);
                return Results.Ok(SessionResponse.From(session));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { error = "not_found" });
            }
            catch (ArgumentOutOfRangeException)
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }
        }).AddEndpointFilter<AuthEndpoints.AntiforgeryFilter>();
    }

    private static void MapModelsAndProviders(RouteGroupBuilder group)
    {
        group.MapGet("/models/image", async (bool? refresh, ModelCatalogService models, CancellationToken cancellationToken) =>
        {
            var values = await models.GetAllImageModelsAsync(refresh == true, cancellationToken);
            return Results.Ok(values.Select(ImageModelResponse.From));
        });

        group.MapGet("/models/assistant", async (bool? refresh, ChatModelCatalogService models, CancellationToken cancellationToken) =>
        {
            var values = await models.ListAllAssistantModelsAsync(refresh == true, cancellationToken);
            return Results.Ok(values.Select(item => AssistantModelResponse.From(item.ProviderId, item.Model)));
        });

        group.MapGet("/providers", async (ProviderRegistry registry, ApiKeyService keys, CancellationToken cancellationToken) =>
        {
            var values = new List<ProviderResponse>(registry.All.Count);
            foreach (var provider in registry.All)
            {
                values.Add(new ProviderResponse(
                    provider.Id,
                    provider.DisplayName,
                    provider.Kind.ToString(),
                    await keys.GetMaskedKeyAsync(provider.Id, cancellationToken)));
            }

            return Results.Ok(values);
        });

        group.MapPut("/providers/{providerId}/key", async (
            string providerId,
            ProviderKeyRequest request,
            ApiKeyService keys,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var status = await keys.SaveAsync(providerId, request.ApiKey, cancellationToken);
                return Results.Ok(new { status = status.ToString() });
            }
            catch (ProviderException exception)
            {
                return Results.Json(new { error = exception.Kind.ToString() }, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }
        }).AddEndpointFilter<AuthEndpoints.AntiforgeryFilter>();

        group.MapDelete("/providers/{providerId}/key", async (string providerId, ProviderRegistry registry, ApiKeyService keys, CancellationToken cancellationToken) =>
        {
            if (!registry.TryGet(providerId, out _))
            {
                return Results.NotFound(new { error = "not_found" });
            }

            try
            {
                await keys.RemoveAsync(providerId, cancellationToken);
                return Results.NoContent();
            }
            catch (ProviderException exception)
            {
                return Results.Json(new { error = exception.Kind.ToString() }, statusCode: StatusCodes.Status404NotFound);
            }
        }).AddEndpointFilter<AuthEndpoints.AntiforgeryFilter>();
    }

    public sealed record RenameRequest(string Title);

    public sealed record ProviderKeyRequest(string ApiKey);

    public sealed record ProviderResponse(string Id, string DisplayName, string Kind, string? MaskedKey);

    public sealed record SessionResponse(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, SessionSettings Settings)
    {
        public static SessionResponse From(Session session) => new(session.Id, session.Title, session.CreatedAt, session.UpdatedAt, session.Settings);
    }

    public sealed record ImageModelResponse(
        string ProviderId,
        string Id,
        string Name,
        bool SupportsSeed,
        bool SupportsImageInput,
        int MaxImagesPerRequest,
        IReadOnlyList<ModelParameter>? Parameters,
        IReadOnlyList<ImageModelPrice> Prices)
    {
        public static ImageModelResponse From(ImageModel model) => new(
            model.ProviderId, model.Id, model.Name, model.SupportsSeed, model.SupportsImageInput, model.MaxImagesPerRequest, model.Parameters, model.Prices);
    }

    public sealed record AssistantModelResponse(string ProviderId, string Id, string Name, bool SupportsImageInput, int? ContextLength, bool SupportsTools)
    {
        public static AssistantModelResponse From(string providerId, ChatModel model) =>
            new(providerId, model.Id, model.Name, model.SupportsImageInput, model.ContextLength, model.SupportsTools);
    }

    public sealed record TimelineResponse(
        SessionResponse Session,
        IReadOnlyList<MessageResponse> Messages,
        IReadOnlyList<RunResponse> Runs,
        IReadOnlyList<GenerationResponse> Generations)
    {
        public static TimelineResponse From(SessionTimeline timeline) => new(
            SessionResponse.From(timeline.Session),
            timeline.Messages.Select(MessageResponse.From).ToList(),
            timeline.Runs.Select(RunResponse.From).ToList(),
            timeline.Generations.Select(GenerationResponse.From).ToList());
    }

    public sealed record MessageResponse(Guid Id, string Role, string Text, DateTimeOffset CreatedAt, Guid? RunId, bool IsError, string? ErrorKind, decimal? CostUsd)
    {
        public static MessageResponse From(ImageGen.Core.Domain.Chat.ChatMessage message) =>
            new(message.Id, message.Role.ToString(), message.Text, message.CreatedAt, message.RunId, message.IsError, message.ErrorKind?.ToString(), message.CostUsd);
    }

    public sealed record RunResponse(Guid Id, Guid SessionId, string Prompt, string Origin, DateTimeOffset CreatedAt)
    {
        public static RunResponse From(Run run) => new(run.Id, run.SessionId, run.Prompt, run.Origin.ToString(), run.CreatedAt);
    }

    public sealed record GenerationResponse(
        Guid Id,
        Guid RunId,
        Guid SessionId,
        string ProviderId,
        string ModelId,
        string Prompt,
        long Seed,
        string Status,
        string? ImagePath,
        string? MediaType,
        decimal? CostUsd,
        string? ErrorKind,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        int SortOrder)
    {
        public static GenerationResponse From(Generation generation) => new(
            generation.Id,
            generation.RunId,
            generation.SessionId,
            generation.ProviderId,
            generation.ModelId,
            generation.Prompt,
            generation.Seed,
            generation.Status.ToString(),
            generation.ImagePath,
            generation.MediaType,
            generation.CostUsd,
            generation.ErrorKind?.ToString(),
            generation.CreatedAt,
            generation.CompletedAt,
            generation.SortOrder);
    }
}
