using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Services;

namespace ImageGen.Core.Context;

/// <summary>
/// Decides when the older part of the history is summarized and which messages it covers (C3.2–C3.3). Pure: no storage
/// and no model calls. Messages with an error are never summarized (C1.2).
/// </summary>
public static class SummaryPlanner
{
    /// <summary>C3.2: the summary is due when the messages after the covered part take more than this share of the budget.</summary>
    public const double TriggerShare = 0.8;

    /// <summary>C3.2: at least this many messages, besides the last <see cref="AssistantPrompts.HistoryMessageCount"/>, must be old enough.</summary>
    public const int MinimumToSummarize = 10;

    /// <summary>Returns the plan, or null when no summary is due yet.</summary>
    /// <param name="messages">The session's messages, oldest first.</param>
    /// <param name="current">The summary the session has now, if any.</param>
    /// <param name="historyBudget">Tokens left for the history (<see cref="ContextBudget.HistoryBudget"/>).</param>
    /// <param name="factor">Correction of the estimate for this model (<see cref="TokenCorrection"/>).</param>
    public static SummaryPlan? Plan(IReadOnlyList<ChatMessage> messages, ConversationSummary? current, int historyBudget, double factor)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var start = 0;
        if (current is not null)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].Id == current.CoversUpToMessageId)
                {
                    start = i + 1;
                    break;
                }
            }
        }

        var candidates = messages.Skip(start).Where(m => !m.IsError).ToList();
        var tokens = candidates.Sum(m => (int)Math.Ceiling(TokenEstimator.Estimate(m.Text) * factor));
        if (tokens <= historyBudget * TriggerShare)
        {
            return null;
        }

        var old = candidates.Count - AssistantPrompts.HistoryMessageCount;
        if (old < MinimumToSummarize)
        {
            return null;
        }

        var toSummarize = candidates.Take(old).ToList();
        return new SummaryPlan(toSummarize, toSummarize[^1].Id);
    }
}
