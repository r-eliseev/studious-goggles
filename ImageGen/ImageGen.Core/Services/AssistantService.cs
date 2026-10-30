using System.Text.Json;
using ImageGen.Core.Context;
using ImageGen.Core.Diagnostics;
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
    ConversationSummaryService summaryService,
    IDebugTrace trace)
{

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
        await messages.AddUserWithAttachmentsAsync(userMessage, attachmentIds, cancellationToken);
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
        if (trace.IsEnabled)
        {
            // The user's words and the attachment ids; the pictures themselves are already in the data folder (task 085).
            trace.Record(new TraceRecord(
                "user-input",
                new Dictionary<string, string?>
                {
                    ["session"] = sessionId.ToString("N"),
                    ["text"] = userMessage.Text,
                    ["attachments"] = string.Join(",", attachmentIds.Select(id => id.ToString("N"))),
                },
                []));
        }

        Run? run = null;
        string replyText;
        ErrorKind? errorKind = null;
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
                    replyText = string.Empty;
                    errorKind = ErrorKind.InvalidToolCall;
                }
                else
                {
                    (run, replyText, errorKind) = await StartRunReplyAsync(sessionId, reply.Text, prompt, attachmentIds, cancellationToken);
                }
            }
            else if (string.IsNullOrWhiteSpace(reply.Text))
            {
                replyText = string.Empty;
                errorKind = ErrorKind.EmptyResponse;
            }
            else
            {
                replyText = reply.Text.Trim();
            }
        }
        catch (ProviderException ex)
        {
            logger.LogWarning("Assistant request failed with error kind {ErrorKind}.", ex.Kind);
            // The provider's own text is kept as a technical detail; the interface shows the text for the code.
            replyText = ex.Message;
            errorKind = ex.Kind;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException && attachmentIds.Count > 0)
        {
            logger.LogWarning(ex, "An attached image of session {SessionId} could not be read.", sessionId);
            replyText = ex.Message;
            errorKind = ErrorKind.AttachmentUnreadable;
        }

        var assistantMessage = NewMessage(sessionId, ChatRole.Assistant, replyText);
        assistantMessage.RunId = run?.Id;
        assistantMessage.IsError = errorKind is not null;
        assistantMessage.ErrorKind = errorKind;
        await messages.AddAsync(assistantMessage, cancellationToken);
        // C3.1: the summary is written in the background; the reply above does not wait for it.
        summaryService.Request(sessionId);
        logger.LogInformation(
            "Assistant turn in session {SessionId} finished: {MessageCount} messages sent, {ToolCallCount} tool calls, run {RunId}, error {IsError}.",
            sessionId,
            messageCount,
            toolCallCount,
            run?.Id,
            errorKind is not null);
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

    private async Task<(Run? Run, string Text, ErrorKind? Error)> StartRunReplyAsync(
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
                return (null, string.Empty, ErrorKind.ModelWithoutReferences);
            }

            // Notes about skipped models and references are shown next to a started run. They are not errors and are
            // still written in English (task 043 covers error texts only).
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
            return (result.Run, string.Join(" ", [text, .. notes]).Trim(), null);
        }
        catch (InvalidOperationException ex)
        {
            // For example, the session has no image models: the code is shown, the core's message is kept as a detail.
            return (null, ex.Message, ErrorKind.ModelUnavailable);
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
        var userCandidates = new List<string>();
        var lastRunCandidates = new List<string>();
        if (userImages.Count > 0 || lastRun is not null)
        {
            if (await chatModels.IsImageInputAsync(assistantModel, cancellationToken))
            {
                userCandidates.AddRange(userImages);
                lastRunCandidates.AddRange(await LastRunImagesAsync(session, lastRun, cancellationToken));
            }
        }

        // The cap of four pictures: the user's own first, the last run's into the room that is left. The cap itself is
        // not a loss of data and is not warned about; only pictures dropped beyond it are.
        var cappedUser = userCandidates.Take(AttachmentService.MaxAttachmentsPerMessage).ToList();
        var cappedLastRun = lastRunCandidates.Take(AttachmentService.MaxAttachmentsPerMessage - cappedUser.Count).ToList();
        var cappedTotal = cappedUser.Count + cappedLastRun.Count;

        var fixedTokens = AssistantPrompts.FixedTokens(currentLine);
        var summaryTokens = summary is null ? 0 : TokenEstimator.Estimate(AssistantPrompts.SummaryLine(summary.Text));
        var contextLength = await chatModels.ContextLengthAsync(assistantModel, cancellationToken);
        var factor = correction.FactorFor(assistantModel);
        var covered = AfterCovered(history, summary);

        // C1.6: the history is shortened first; a picture is dropped only when the request still does not fit. The
        // pictures of the last run go first, from the end, then the user's own ones.
        var userCount = cappedUser.Count;
        var lastRunCount = cappedLastRun.Count;
        List<string> attachedImages;
        IReadOnlyList<ContextMessage> selected;
        var fits = false;
        while (true)
        {
            attachedImages = cappedUser.Take(userCount).Concat(cappedLastRun.Take(lastRunCount)).ToList();
            var budget = new AssistantRequestBudget(
                contextLength,
                fixedTokens,
                TokenEstimator.EstimateImages(attachedImages.Count),
                summaryTokens,
                factor);
            selected = ContextBuilder.Select(covered, budget.HistoryBudget, factor);
            if (budget.CorrectedEstimate(selected) <= contextLength)
            {
                fits = true;
                break;
            }

            if (attachedImages.Count == 0)
            {
                // The mandatory part alone does not fit: the current message stays, the pictures are already gone.
                break;
            }

            if (lastRunCount > 0)
            {
                lastRunCount--;
            }
            else
            {
                userCount--;
            }
        }

        var removed = cappedTotal - attachedImages.Count;
        if (removed > 0 || !fits)
        {
            logger.LogWarning(
                "Pictures of session {SessionId} were dropped for provider {ProviderId} and model {ModelId}: {ImagesRemoved} removed, {ImagesKept} kept.",
                session.Id,
                assistantModel.ProviderId,
                assistantModel.ModelId,
                removed,
                attachedImages.Count);
        }

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
