using System.Text.Json;
using ImageGen.Core.Context;
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
    ChatModelCatalogService chatModels,
    IImageFileStore files,
    AttachmentService attachments,
    TimeProvider timeProvider,
    ILogger<AssistantService> logger,
    TokenCorrection correction,
    IConversationSummaryRepository summaries,
    ConversationSummaryService summaryService)
{
    private const string EmptyResponseText = "The assistant returned an empty response.";
    private const string InvalidToolCallText = "The assistant returned an invalid tool call.";

    public Task<AssistantTurnResult> SendUserMessageAsync(Guid sessionId, string text, CancellationToken cancellationToken) =>
        SendUserMessageAsync(sessionId, text, [], cancellationToken);

    /// <summary>
    /// Sends the user's message with its attached images (task 018). The text may be empty when images are attached.
    /// The images go to the assistant (when its model takes images) and become reference images of a run it starts.
    /// </summary>
    public async Task<AssistantTurnResult> SendUserMessageAsync(
        Guid sessionId,
        string text,
        IReadOnlyList<Guid> attachmentIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(attachmentIds);
        if (attachmentIds.Count > AttachmentService.MaxAttachmentsPerMessage)
        {
            throw new ArgumentException(
                $"At most {AttachmentService.MaxAttachmentsPerMessage} images can be attached to a message.", nameof(attachmentIds));
        }

        if (attachmentIds.Count == 0)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
        }

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
        await attachments.LinkToMessageAsync(attachmentIds, userMessage.Id, cancellationToken);
        if (trimmed.Length > 0)
        {
            await sessions.SetTitleFromFirstMessageAsync(sessionId, trimmed, cancellationToken);
        }

        return await CompleteTurnAsync(session, userMessage, attachmentIds, cancellationToken);
    }

    /// <summary>
    /// Sends the last user message, with its images, to the assistant again (S2.5, task 077). Used after an error reply: no
    /// user message is added, and the error reply stays in the history but is left out of the request (C1.2).
    /// </summary>
    public async Task<AssistantTurnResult> RetryLastTurnAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");
        var history = await messages.ListBySessionAsync(sessionId, cancellationToken);
        var userMessage = history.LastOrDefault(m => m.Role == ChatRole.User)
            ?? throw new InvalidOperationException($"Session {sessionId} has no user message to send again.");
        var attachmentIds = await attachments.ListIdsByMessageAsync(userMessage.Id, cancellationToken);
        return await CompleteTurnAsync(session, userMessage, attachmentIds, cancellationToken);
    }

    /// <summary>Builds the request, calls the assistant, starts a run if it asks for one, and saves its reply.</summary>
    private async Task<AssistantTurnResult> CompleteTurnAsync(
        Session session,
        ChatMessage userMessage,
        IReadOnlyList<Guid> attachmentIds,
        CancellationToken cancellationToken)
    {
        var sessionId = session.Id;
        Run? run = null;
        string replyText;
        var isError = false;
        var messageCount = 0;
        var toolCallCount = 0;
        try
        {
            var chatClient = providers.GetChatClient(session.Settings.AssistantModel.ProviderId);
            var userImages = new List<string>(attachmentIds.Count);
            foreach (var attachmentId in attachmentIds)
            {
                userImages.Add(await attachments.ReadDataUrlAsync(attachmentId, cancellationToken));
            }

            var built = await BuildRequestAsync(session, userImages, cancellationToken);
            var request = built.Request;
            messageCount = request.Messages.Count;
            var reply = await chatClient.CompleteChatAsync(request, cancellationToken);
            if (reply.PromptTokens is { } promptTokens)
            {
                // C2.1: the next request of this model is estimated with the factor this reply gives.
                correction.Record(session.Settings.AssistantModel, built.EstimatedTokens, promptTokens);
            }
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
                    (run, replyText, isError) = await StartRunReplyAsync(sessionId, reply.Text, prompt, attachmentIds, cancellationToken);
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
        catch (Exception ex) when (ex is IOException or InvalidOperationException && attachmentIds.Count > 0)
        {
            logger.LogWarning(ex, "An attached image of session {SessionId} could not be read.", sessionId);
            replyText = "An attached image could not be read.";
            isError = true;
        }

        var assistantMessage = NewMessage(sessionId, ChatRole.Assistant, replyText);
        assistantMessage.RunId = run?.Id;
        assistantMessage.IsError = isError;
        await messages.AddAsync(assistantMessage, cancellationToken);
        // C3.1: the summary is written in the background; the reply above does not wait for it.
        summaryService.Request(sessionId);
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
        IReadOnlyList<Guid> attachmentIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await generations.StartRunWithAttachmentsAsync(sessionId, prompt, attachmentIds, cancellationToken);
            if (result.Run is null)
            {
                return (null, SkippedText(result.SkippedModels), true);
            }

            var notes = new List<string>();
            if (result.SkippedModels.Count > 0)
            {
                notes.Add(SkippedText(result.SkippedModels));
            }

            if (result.ReferencesNotSent > 0)
            {
                notes.Add($"{result.ReferencesNotSent} attached image(s) were not sent: over the model's reference limit.");
            }

            var text = AssistantPrompts.RunMessage(assistantText);
            return (result.Run, string.Join(" ", [text, .. notes]).Trim(), false);
        }
        catch (InvalidOperationException ex)
        {
            // For example, the session has no image models. The message is written by the core and has no secrets.
            return (null, ex.Message, true);
        }
    }

    private static string SkippedText(IReadOnlyList<SkippedModel> skipped) =>
        "No run was started for " + string.Join(", ", skipped.Select(s => s.Model.ModelId))
        + ": the model does not take reference images.";

    /// <summary>The request and its estimated size before the correction (for <see cref="TokenCorrection"/>).</summary>
    private sealed record BuiltRequest(AssistantChatRequest Request, int EstimatedTokens);

    private async Task<BuiltRequest> BuildRequestAsync(Session session, IReadOnlyList<string> userImages, CancellationToken cancellationToken)
    {
        var history = await messages.ListBySessionAsync(session.Id, cancellationToken);
        var summary = await summaries.GetAsync(session.Id, cancellationToken);
        var lastRun = (await runs.ListRunsBySessionAsync(session.Id, cancellationToken)).LastOrDefault();
        var assistantModel = session.Settings.AssistantModel;
        var currentLine = lastRun is null ? AssistantPrompts.NoCurrentPrompt : AssistantPrompts.CurrentPrompt(lastRun.Prompt);

        // Images reach the assistant only when its model takes them: the user's own images first, then the last run's.
        var images = new List<string>();
        if (userImages.Count > 0 || lastRun is not null)
        {
            if (await chatModels.IsImageInputAsync(assistantModel, cancellationToken))
            {
                images.AddRange(userImages);
                images.AddRange(await LastRunImagesAsync(session, lastRun, cancellationToken));
            }
        }

        // C1.1: the history gets what the model's context leaves after the fixed part, the pictures and the reserve.
        var attachedImages = images.Take(AttachmentService.MaxAttachmentsPerMessage).ToList();
        var fixedTokens = AssistantPrompts.FixedTokens(currentLine);
        var summaryTokens = summary is null ? 0 : TokenEstimator.Estimate(AssistantPrompts.SummaryLine(summary.Text));
        var contextLength = await chatModels.ContextLengthAsync(assistantModel, cancellationToken);
        var budget = new ContextBudget(contextLength, fixedTokens, TokenEstimator.EstimateImages(attachedImages.Count), summaryTokens);
        var selected = ContextBuilder.Select(AfterCovered(history, summary), budget.HistoryBudget, correction.FactorFor(assistantModel));

        var chat = new List<AssistantChatMessage>
        {
            AssistantChatMessage.System(AssistantPrompts.System),
            AssistantChatMessage.System(currentLine),
        };
        if (summary is not null)
        {
            chat.Add(AssistantChatMessage.System(AssistantPrompts.SummaryLine(summary.Text)));
        }

        foreach (var message in selected)
        {
            chat.Add(message.Role == ChatRole.User
                ? AssistantChatMessage.User(message.Text)
                : AssistantChatMessage.Assistant(message.Text));
        }

        var currentUser = chat.FindLastIndex(m => m.Role == AssistantChatMessage.UserRole);
        if (attachedImages.Count > 0 && currentUser >= 0)
        {
            chat[currentUser] = new AssistantChatMessage(AssistantChatMessage.UserRole, chat[currentUser].Text, attachedImages);
        }

        var tool = new AssistantTool(
            AssistantPrompts.GenerateImagesToolName,
            AssistantPrompts.GenerateImagesToolDescription,
            AssistantPrompts.GenerateImagesToolSchema);
        var estimated = fixedTokens + summaryTokens
            + selected.Sum(m => TokenEstimator.Estimate(m.Text))
            + TokenEstimator.EstimateImages(attachedImages.Count);
        return new BuiltRequest(new AssistantChatRequest(assistantModel.ModelId, chat, [tool]), estimated);
    }

    /// <summary>C1.3: the history after the part a summary covers. A summary whose last message is gone covers nothing.</summary>
    private static IReadOnlyList<ChatMessage> AfterCovered(IReadOnlyList<ChatMessage> history, ConversationSummary? summary)
    {
        if (summary is null)
        {
            return history;
        }

        for (var i = 0; i < history.Count; i++)
        {
            if (history[i].Id == summary.CoversUpToMessageId)
            {
                return history.Skip(i + 1).ToList();
            }
        }

        return history;
    }

    /// <summary>
    /// Images of the last run for the assistant (S2.4), as data URLs. Only when the assistant model takes images. A missing
    /// file is skipped with a warning; the other images are still sent.
    /// </summary>
    private async Task<List<string>> LastRunImagesAsync(Session session, Run? lastRun, CancellationToken cancellationToken)
    {
        var images = new List<string>();
        if (lastRun is null)
        {
            return images;
        }

        var generations = (await runs.ListGenerationsBySessionAsync(session.Id, cancellationToken))
            .Where(g => g.RunId == lastRun.Id);
        foreach (var generation in RunImageSelector.Select(generations))
        {
            try
            {
                var content = await files.ReadAsync(generation.ImagePath!, cancellationToken);
                images.Add(ImageDataUrl.From(content, generation.MediaType ?? "image/png"));
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Image {ImagePath} of run {RunId} could not be read; the assistant does not get it.", generation.ImagePath, lastRun.Id);
            }
        }

        return images;
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
