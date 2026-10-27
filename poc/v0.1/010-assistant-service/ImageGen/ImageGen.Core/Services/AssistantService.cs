using System.Text.Json;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>Chat turn: saves the user message, asks the assistant model, starts a run when it calls generate_images.</summary>
public sealed class AssistantService(
    SessionService sessionService,
    IChatMessageRepository messages,
    IRunRepository runs,
    IOpenRouterChatClient client,
    GenerationService generationService,
    TimeProvider timeProvider,
    ILogger<AssistantService> logger)
{
    public async Task<AssistantTurnResult> SendUserMessageAsync(Guid sessionId, string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var session = await sessionService.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = ChatRole.User,
            Text = text.Trim(),
            CreatedAt = timeProvider.GetUtcNow(),
        };
        await messages.AddAsync(userMessage, cancellationToken);
        await sessionService.SetTitleFromFirstMessageAsync(sessionId, userMessage.Text, cancellationToken);

        ChatMessage assistantMessage;
        Run? run = null;
        try
        {
            var request = await BuildRequestAsync(session, cancellationToken);
            var reply = await client.CompleteChatAsync(request, cancellationToken);
            var toolCall = reply.ToolCalls.FirstOrDefault(c => c.Name == AssistantPrompts.GenerateImagesToolName);
            if (toolCall is not null)
            {
                var prompt = ReadPrompt(toolCall.ArgumentsJson);
                if (prompt is null)
                {
                    assistantMessage = CreateAssistantMessage(sessionId, "The assistant returned an invalid tool call.", isError: true);
                }
                else
                {
                    try
                    {
                        run = await generationService.StartRunAsync(sessionId, prompt, cancellationToken);
                        assistantMessage = CreateAssistantMessage(sessionId, AssistantPrompts.RunMessage(reply.Text, prompt), isError: false);
                        assistantMessage.RunId = run.Id;
                    }
                    catch (InvalidOperationException ex)
                    {
                        assistantMessage = CreateAssistantMessage(sessionId, ex.Message, isError: true);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(reply.Text))
            {
                assistantMessage = CreateAssistantMessage(sessionId, reply.Text.Trim(), isError: false);
            }
            else
            {
                assistantMessage = CreateAssistantMessage(sessionId, "The assistant returned an empty response.", isError: true);
            }
        }
        catch (ProviderException ex)
        {
            logger.LogWarning("Assistant request failed: {Kind}", ex.Kind);
            assistantMessage = CreateAssistantMessage(sessionId, $"Assistant error ({ex.Kind}): {ex.Message}", isError: true);
        }

        await messages.AddAsync(assistantMessage, cancellationToken);
        return new AssistantTurnResult(userMessage, assistantMessage, run);
    }

    private async Task<OpenRouterChatRequest> BuildRequestAsync(Session session, CancellationToken cancellationToken)
    {
        var sessionRuns = await runs.ListRunsBySessionAsync(session.Id, cancellationToken);
        var currentPrompt = sessionRuns.Count == 0
            ? AssistantPrompts.NoCurrentPrompt
            : AssistantPrompts.CurrentPrompt(sessionRuns[^1].Prompt);

        var history = (await messages.ListBySessionAsync(session.Id, cancellationToken))
            .Where(m => !m.IsError)
            .TakeLast(AssistantPrompts.HistoryMessageCount)
            .Select(m => m.Role == ChatRole.User
                ? OpenRouterChatMessage.User(m.Text)
                : OpenRouterChatMessage.Assistant(m.Text));

        var chat = new List<OpenRouterChatMessage>
        {
            OpenRouterChatMessage.System(AssistantPrompts.System),
            OpenRouterChatMessage.System(currentPrompt),
        };
        chat.AddRange(history);

        return new OpenRouterChatRequest(
            session.Settings.AssistantModelId,
            chat,
            [new OpenRouterTool(
                AssistantPrompts.GenerateImagesToolName,
                AssistantPrompts.GenerateImagesToolDescription,
                AssistantPrompts.GenerateImagesToolSchema)]);
    }

    private ChatMessage CreateAssistantMessage(Guid sessionId, string text, bool isError) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        Role = ChatRole.Assistant,
        Text = text,
        CreatedAt = timeProvider.GetUtcNow(),
        IsError = isError,
    };

    /// <summary>Reads the "prompt" string from tool arguments. Returns null when missing or empty.</summary>
    public static string? ReadPrompt(string argumentsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("prompt", out var prompt)
                && prompt.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(prompt.GetString()))
            {
                return prompt.GetString()!.Trim();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
