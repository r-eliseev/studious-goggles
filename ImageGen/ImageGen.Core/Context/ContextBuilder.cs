using ImageGen.Core.Domain.Chat;

namespace ImageGen.Core.Context;

/// <summary>A message of the history as it goes into the request. <see cref="Shortened"/> marks a cut text (C1.5).</summary>
public sealed record ContextMessage(ChatRole Role, string Text, bool Shortened);

/// <summary>
/// Chooses the history of the assistant request (C1.2–C1.5): messages with an error are left out; from the newest to the
/// oldest, while they fit in the budget; a message longer than half the budget is shortened (first 2/3 and last 1/3 of
/// the allowed length). The current user message (the newest) is always kept, as written.
/// </summary>
public static class ContextBuilder
{
    private const string ShortenedMarker = "\n[… shortened for the assistant …]\n";

    /// <param name="messages">The session's messages, oldest first.</param>
    /// <param name="historyBudget">Tokens left for the history.</param>
    /// <param name="factor">Correction of the estimate for this model (<see cref="TokenCorrection"/>).</param>
    public static IReadOnlyList<ContextMessage> Select(IReadOnlyList<ChatMessage> messages, int historyBudget, double factor)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var eligible = messages.Where(m => !m.IsError).ToList();
        var selected = new List<ContextMessage>();
        var used = 0;

        for (var i = eligible.Count - 1; i >= 0; i--)
        {
            var message = eligible[i];
            var isCurrent = i == eligible.Count - 1;
            // The current message is sent as written: shortening it would change what the user asked.
            var (text, shortened) = isCurrent ? (message.Text, false) : Fit(message.Text, historyBudget);
            var cost = (int)Math.Ceiling(TokenEstimator.Estimate(text) * factor);
            if (!isCurrent && used + cost > historyBudget)
            {
                break;
            }

            selected.Add(new ContextMessage(message.Role, text, shortened));
            used += cost;
        }

        selected.Reverse();
        return selected;
    }

    private static (string Text, bool Shortened) Fit(string text, int historyBudget)
    {
        var maxTokens = historyBudget / 2;
        if (TokenEstimator.Estimate(text) <= maxTokens)
        {
            return (text, false);
        }

        var allowedChars = Math.Max(0, (int)(maxTokens * 3.5) - ShortenedMarker.Length);
        var head = allowedChars * 2 / 3;
        var tail = allowedChars - head;
        return (text[..head] + ShortenedMarker + text[^tail..], true);
    }
}
