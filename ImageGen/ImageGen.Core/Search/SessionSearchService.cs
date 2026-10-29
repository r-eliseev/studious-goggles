namespace ImageGen.Core.Search;

/// <summary>
/// Finds sessions by their titles, prompts and picture descriptions. The texts are read in one call and compared in memory
/// (one user, a small history). A title counts three times as much as a prompt, and a prompt twice as much as a description.
/// Each session appears once, with its best hit.
/// </summary>
public sealed class SessionSearchService(ISearchTextSource source)
{
    /// <summary>Length of a snippet with its ellipses.</summary>
    public const int SnippetLength = 80;

    private const int SnippetBodyLength = SnippetLength - 2;
    private const int SnippetLead = 20;

    /// <summary>Hits sorted by score, highest first. An empty or blank query returns no hits.</summary>
    public async Task<IReadOnlyList<SessionSearchHit>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var tokens = FuzzyMatcher.Tokenize(query);
        if (tokens.Count == 0)
        {
            return [];
        }

        var texts = await source.ListAllAsync(cancellationToken);
        var best = new Dictionary<Guid, SessionSearchHit>();
        foreach (var item in texts)
        {
            var match = FuzzyMatcher.Match(tokens, item.Text);
            if (match is null)
            {
                continue;
            }

            var score = match.Value.Score * Weight(item.Kind);
            if (best.TryGetValue(item.SessionId, out var current) && current.Score >= score)
            {
                continue;
            }

            best[item.SessionId] = new SessionSearchHit(item.SessionId, score, item.Kind, Snippet(item.Text, match.Value.FirstIndex));
        }

        return best.Values.OrderByDescending(hit => hit.Score).ToList();
    }

    private static int Weight(SearchTextKind kind) => kind switch
    {
        SearchTextKind.Title => 3,
        SearchTextKind.Prompt => 2,
        _ => 1,
    };

    /// <summary>A fragment of at most <see cref="SnippetLength"/> characters around <paramref name="index"/>, with «…» where cut.</summary>
    private static string Snippet(string text, int index)
    {
        if (text.Length <= SnippetLength)
        {
            return text;
        }

        var start = Math.Clamp(index - SnippetLead, 0, text.Length - SnippetBodyLength);
        var end = start + SnippetBodyLength;
        var head = start > 0 ? "…" : string.Empty;
        var tail = end < text.Length ? "…" : string.Empty;
        return head + text[start..end] + tail;
    }
}
