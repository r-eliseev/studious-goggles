using System.Text.RegularExpressions;

namespace ImageGen.Core.Search;

/// <summary>
/// Case-insensitive matching of query words against a text. A query word scores 3 when a word of the text starts with it,
/// 2 when a word contains it, and 1 when a word, or a beginning of the word with the same first letter, differs by one
/// letter (only for words of four letters or more). Every query word must match, otherwise the text does not match.
/// </summary>
public static class FuzzyMatcher
{
    private const int PrefixScore = 3;
    private const int SubstringScore = 2;
    private const int TypoScore = 1;
    private const int MinTypoLength = 4;

    private static readonly Regex WordPattern = new(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant);

    /// <summary>Lowercase words of the text, split at every character that is neither a letter nor a digit.</summary>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return WordPattern.Matches(text.ToLowerInvariant()).Select(match => match.Value).ToList();
    }

    /// <summary>Matches every query token against the words of <paramref name="text"/>. Returns null when one token is missing.</summary>
    public static FuzzyMatch? Match(IReadOnlyList<string> queryTokens, string text)
    {
        ArgumentNullException.ThrowIfNull(queryTokens);
        ArgumentNullException.ThrowIfNull(text);
        var lowered = text.ToLowerInvariant();
        var words = WordPattern.Matches(lowered).Select(match => (Word: match.Value, Start: match.Index)).ToList();
        var total = 0;
        var firstIndex = int.MaxValue;
        foreach (var token in queryTokens)
        {
            var best = BestWordMatch(token, words);
            if (best is null)
            {
                return null;
            }

            total += best.Value.Score;
            firstIndex = Math.Min(firstIndex, best.Value.Index);
        }

        return queryTokens.Count == 0 ? null : new FuzzyMatch(total, firstIndex);
    }

    private static (int Score, int Index)? BestWordMatch(string token, IReadOnlyList<(string Word, int Start)> words)
    {
        (int Score, int Index)? best = null;
        foreach (var (word, start) in words)
        {
            var candidate = ScoreWord(token, word, start);
            if (candidate is not null && (best is null || candidate.Value.Score > best.Value.Score))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static (int Score, int Index)? ScoreWord(string token, string word, int start)
    {
        if (word.StartsWith(token, StringComparison.Ordinal))
        {
            return (PrefixScore, start);
        }

        var position = word.IndexOf(token, StringComparison.Ordinal);
        if (position >= 0)
        {
            return (SubstringScore, start + position);
        }

        if (IsTypo(token, word))
        {
            return (TypoScore, start);
        }

        return null;
    }

    /// <summary>True when the token differs from the word, or from a beginning of the word with the same first letter,
    /// by one letter. Tokens shorter than four letters are never treated as a typo.</summary>
    private static bool IsTypo(string token, string word)
    {
        if (token.Length < MinTypoLength)
        {
            return false;
        }

        if (Distance(token, word) <= 1)
        {
            return true;
        }

        if (token[0] != word[0])
        {
            return false;
        }

        foreach (var length in new[] { token.Length - 1, token.Length, token.Length + 1 })
        {
            if (length > 0 && length < word.Length && Distance(token, word[..length]) <= 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Levenshtein distance between two words (insertions, deletions and substitutions cost 1).</summary>
    private static int Distance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
