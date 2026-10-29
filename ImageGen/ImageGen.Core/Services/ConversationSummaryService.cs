using ImageGen.Core.Context;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Providers;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Services;

/// <summary>
/// Writes the rolling summary of a session's older history (C3.1, ADR 0015) in the background: the assistant's reply does
/// not wait for it. One job per session at a time. A failed job is logged; the next reply starts a new one (C3.5).
/// </summary>
public sealed class ConversationSummaryService(
    ISessionRepository sessions,
    IChatMessageRepository messages,
    IRunRepository runs,
    IConversationSummaryRepository summaries,
    IProviderRouter providers,
    ChatModelCatalogService chatModels,
    TokenCorrection correction,
    TimeProvider timeProvider,
    ILogger<ConversationSummaryService> logger)
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Task> _running = [];

    /// <summary>Starts a summary job for the session unless one is already running. Returns at once.</summary>
    public void Request(Guid sessionId)
    {
        lock (_gate)
        {
            if (_running.ContainsKey(sessionId))
            {
                return;
            }

            var job = RunAsync(sessionId);
            _running[sessionId] = job;
            // The continuation waits for the lock, so the job is registered before it can remove itself.
            _ = job.ContinueWith(_ =>
            {
                lock (_gate)
                {
                    _running.Remove(sessionId);
                }
            }, TaskScheduler.Default);
        }
    }

    /// <summary>Completes when every running summary job has finished. Tests wait on it.</summary>
    public Task WhenIdleAsync()
    {
        Task[] jobs;
        lock (_gate)
        {
            jobs = _running.Values.ToArray();
        }

        return Task.WhenAll(jobs);
    }

    private async Task RunAsync(Guid sessionId)
    {
        try
        {
            var session = await sessions.GetAsync(sessionId, CancellationToken.None);
            if (session is null)
            {
                return;
            }

            var model = session.Settings.AssistantModel;
            var history = await messages.ListBySessionAsync(sessionId, CancellationToken.None);
            var current = await summaries.GetAsync(sessionId, CancellationToken.None);
            var lastRun = (await runs.ListRunsBySessionAsync(sessionId, CancellationToken.None)).LastOrDefault();
            var currentLine = lastRun is null ? AssistantPrompts.NoCurrentPrompt : AssistantPrompts.CurrentPrompt(lastRun.Prompt);
            var budget = new ContextBudget(
                await chatModels.ContextLengthAsync(model, CancellationToken.None),
                AssistantPrompts.FixedTokens(currentLine),
                ImageTokens: 0,
                SummaryTokens: current is null ? 0 : TokenEstimator.Estimate(AssistantPrompts.SummaryLine(current.Text)));
            var plan = SummaryPlanner.Plan(history, current, budget.HistoryBudget, correction.FactorFor(model));
            if (plan is null)
            {
                return;
            }

            var chat = new List<AssistantChatMessage> { AssistantChatMessage.System(AssistantPrompts.SummaryInstruction) };
            if (current is not null)
            {
                chat.Add(AssistantChatMessage.User(AssistantPrompts.SummaryLine(current.Text)));
            }

            foreach (var message in plan.ToSummarize)
            {
                chat.Add(message.Role == ChatRole.User
                    ? AssistantChatMessage.User(message.Text)
                    : AssistantChatMessage.Assistant(message.Text));
            }

            var reply = await providers.GetChatClient(model.ProviderId)
                .CompleteChatAsync(new AssistantChatRequest(model.ModelId, chat, []), CancellationToken.None);
            var text = reply.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                logger.LogWarning("Summary of session {SessionId} came back empty; the previous summary is kept.", sessionId);
                return;
            }

            await summaries.UpsertAsync(new ConversationSummary
            {
                SessionId = sessionId,
                Text = text,
                CoversUpToMessageId = plan.CoversUpToMessageId,
                Model = model,
                CostUsd = reply.CostUsd,
                CreatedAt = timeProvider.GetUtcNow(),
            }, CancellationToken.None);
            logger.LogInformation("Summary of session {SessionId} covers {MessageCount} messages.", sessionId, plan.ToSummarize.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Summary of session {SessionId} failed; the next reply starts it again.", sessionId);
        }
    }
}
