using System.Text.Json;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>
/// Chat with the session assistant. A <c>generate_images</c> tool call starts a run at once;
/// a text reply is shown as is; a failure is saved as an assistant message with <see cref="ChatMessage.IsError"/>.
/// </summary>
public sealed class AssistantService(
    SessionService sessions,
    IChatMessageRepository messages,
    IRunRepository runs,
    IProviderRouter providers,
    GenerationService generations,
    TimeProvider timeProvider,
    ILogger<AssistantService> logger)
{
    private const string EmptyResponseText = "The assistant returned an empty response.";
    private const string InvalidToolCallText = "The assistant returned an invalid tool call.";

    public async Task<AssistantTurnResult> SendUserMessageAsync(Guid sessionId, string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var session = await sessions.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        var trimmed = text.Trim();
        logger.LogInformation(
            "Assistant turn in session {SessionId} with model {ProviderId}/{ModelId}.",
            sessionId,
            session.Settings.AssistantModel.ProviderId,
            session.Settings.AssistantModel.ModelId);
        logger.LogDebug("User message in session {SessionId}: {Text}", sessionId, trimmed);
        var userMessage = NewMessage(sessionId, ChatRole.User, trimmed);
        await messages.AddAsync(userMessage, cancellationToken);
        await sessions.SetTitleFromFirstMessageAsync(sessionId, trimmed, cancellationToken);

        Run? run = null;
        string replyText;
        var isError = false;
        var messageCount = 0;
        var toolCallCount = 0;
        try
        {
            var chatClient = providers.GetChatClient(session.Settings.AssistantModel.ProviderId);
            var request = await BuildRequestAsync(session, cancellationToken);
            messageCount = request.Messages.Count;
            var reply = await chatClient.CompleteChatAsync(request, cancellationToken);
            toolCallCount = reply.ToolCalls.Count;
            logger.LogDebug("Assistant reply in session {SessionId}: {Text}", sessionId, reply.Text);
            var call = reply.ToolCalls.FirstOrDefault(c => c.Name == AssistantPrompts.GenerateImagesToolName);
            if (call is not null)
            {
                var prompt = ReadPrompt(call.ArgumentsJson);
                if (prompt is null)
                {
                    replyText = InvalidToolCallText;
                    isError = true;
                }
                else
                {
                    (run, replyText, isError) = await StartRunReplyAsync(sessionId, reply.Text, prompt, cancellationToken);
                }
            }
            else if (string.IsNullOrWhiteSpace(reply.Text))
            {
                replyText = EmptyResponseText;
                isError = true;
            }
            else
            {
                replyText = reply.Text.Trim();
            }
        }
        catch (ProviderException ex)
        {
            logger.LogWarning("Assistant request failed with error kind {ErrorKind}.", ex.Kind);
            replyText = $"Assistant error ({ex.Kind}): {ex.Message}";
            isError = true;
        }

        var assistantMessage = NewMessage(sessionId, ChatRole.Assistant, replyText);
        assistantMessage.RunId = run?.Id;
        assistantMessage.IsError = isError;
        await messages.AddAsync(assistantMessage, cancellationToken);
        logger.LogInformation(
            "Assistant turn in session {SessionId} finished: {MessageCount} messages sent, {ToolCallCount} tool calls, run {RunId}, error {IsError}.",
            sessionId,
            messageCount,
            toolCallCount,
            run?.Id,
            isError);
        return new AssistantTurnResult(userMessage, assistantMessage, run);
    }

    /// <summary>Reads the <c>prompt</c> string from the tool arguments. Returns null for any invalid or empty value.</summary>
    internal static string? ReadPrompt(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("prompt", out var value)
                || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var prompt = value.GetString()?.Trim();
            return string.IsNullOrEmpty(prompt) ? null : prompt;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<(Run? Run, string Text, bool IsError)> StartRunReplyAsync(
        Guid sessionId,
        string? assistantText,
        string prompt,
        CancellationToken cancellationToken)
    {
        try
        {
            var run = await generations.StartRunAsync(sessionId, prompt, cancellationToken);
            return (run, AssistantPrompts.RunMessage(assistantText, prompt), false);
        }
        catch (InvalidOperationException ex)
        {
            // For example, the session has no image models. The message is written by the core and has no secrets.
            return (null, ex.Message, true);
        }
    }

    private async Task<AssistantChatRequest> BuildRequestAsync(Session session, CancellationToken cancellationToken)
    {
        var history = await messages.ListBySessionAsync(session.Id, cancellationToken);
        var lastRun = (await runs.ListRunsBySessionAsync(session.Id, cancellationToken)).LastOrDefault();

        var chat = new List<AssistantChatMessage>
        {
            AssistantChatMessage.System(AssistantPrompts.System),
            AssistantChatMessage.System(lastRun is null
                ? AssistantPrompts.NoCurrentPrompt
                : AssistantPrompts.CurrentPrompt(lastRun.Prompt)),
        };
        foreach (var message in history.Where(m => !m.IsError).TakeLast(AssistantPrompts.HistoryMessageCount))
        {
            chat.Add(message.Role == ChatRole.User
                ? AssistantChatMessage.User(message.Text)
                : AssistantChatMessage.Assistant(message.Text));
        }

        var tool = new AssistantTool(
            AssistantPrompts.GenerateImagesToolName,
            AssistantPrompts.GenerateImagesToolDescription,
            AssistantPrompts.GenerateImagesToolSchema);
        return new AssistantChatRequest(session.Settings.AssistantModel.ModelId, chat, [tool]);
    }

    private ChatMessage NewMessage(Guid sessionId, ChatRole role, string text) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        Role = role,
        Text = text,
        CreatedAt = timeProvider.GetUtcNow(),
    };
}
