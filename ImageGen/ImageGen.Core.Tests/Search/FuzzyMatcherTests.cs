using ImageGen.Core.Search;

namespace ImageGen.Core.Tests.Search;

public class FuzzyMatcherTests
{
    [Fact]
    public void Match_Prefix_Scores3()
    {
        var match = FuzzyMatcher.Match(["light"], "Lighthouse on a rock");

        Assert.Equal(3, match!.Value.Score);
        Assert.Equal(0, match.Value.FirstIndex);
    }

    [Fact]
    public void Match_Substring_Scores2()
    {
        var match = FuzzyMatcher.Match(["house"], "Lighthouse on a rock");

        Assert.Equal(2, match!.Value.Score);
        Assert.Equal(5, match.Value.FirstIndex);
    }

    [Fact]
    public void Match_OneTypoLongWord_Scores1()
    {
        var match = FuzzyMatcher.Match(["lighthose"], "Lighthouse on a rock");

        Assert.Equal(1, match!.Value.Score);
    }

    [Fact]
    public void Match_TypoShortWord_NoMatch()
    {
        Assert.Null(FuzzyMatcher.Match(["fux"], "red fox"));
    }

    [Fact]
    public void Match_AllTokensRequired()
    {
        Assert.Null(FuzzyMatcher.Match(["red", "fox"], "red car"));
    }

    [Fact]
    public void Match_AllTokensFound_SumsScores()
    {
        var match = FuzzyMatcher.Match(["red", "fox"], "red fox in snow");

        Assert.Equal(6, match!.Value.Score);
    }

    [Fact]
    public void Tokenize_SplitsPunctuationAndLowercases()
    {
        Assert.Equal(["red", "fox", "2026"], FuzzyMatcher.Tokenize("Red-FOX, 2026!"));
    }

    [Fact]
    public void Match_Cyrillic_Works()
    {
        var match = FuzzyMatcher.Match(FuzzyMatcher.Tokenize("маяк"), "Высокий Маяк на скале");

        Assert.Equal(3, match!.Value.Score);
        Assert.Equal(8, match.Value.FirstIndex);
    }

    [Fact]
    public void Match_TypoInPrefix_Scores1()
    {
        var match = FuzzyMatcher.Match(["watte"], "water lily");

        Assert.Equal(1, match!.Value.Score);
        Assert.Equal(0, match.Value.FirstIndex);
    }

    [Fact]
    public void Match_TypoInPrefixOfLongWord_Scores1()
    {
        var match = FuzzyMatcher.Match(["lighthu"], "Lighthouse on a rock");

        Assert.Equal(1, match!.Value.Score);
    }

    [Fact]
    public void Match_MissingLetterInPrefix_Scores1()
    {
        var match = FuzzyMatcher.Match(["brige"], "two bridges");

        Assert.Equal(1, match!.Value.Score);
        Assert.Equal(4, match.Value.FirstIndex);
    }

    [Fact]
    public void Match_TypoInPrefixEveryToken_Matches()
    {
        var match = FuzzyMatcher.Match(["red", "watte"], "red water");

        Assert.Equal(4, match!.Value.Score);
    }

    [Fact]
    public void Match_ExactPrefix_StillScores3()
    {
        var match = FuzzyMatcher.Match(["wate"], "water lily");

        Assert.Equal(3, match!.Value.Score);
    }

    [Fact]
    public void Match_TypoInPrefixOtherFirstLetter_NoMatch()
    {
        Assert.Null(FuzzyMatcher.Match(["watte"], "attempt"));
    }

    [Fact]
    public void Match_TwoTyposInPrefix_NoMatch()
    {
        Assert.Null(FuzzyMatcher.Match(["watte"], "watch tower"));
    }

    [Fact]
    public void Match_DifferentEnding_NoMatch()
    {
        Assert.Null(FuzzyMatcher.Match(["house"], "hound"));
    }

    [Fact]
    public void Match_SwappedLetters_NoMatch()
    {
        Assert.Null(FuzzyMatcher.Match(["wtaer"], "water lily"));
    }

    [Fact]
    public void Match_ShortTokenTypo_NoMatch()
    {
        Assert.Null(FuzzyMatcher.Match(["wot"], "water lily"));
    }

    [Fact]
    public void Match_EmptyQuery_ReturnsNull()
    {
        Assert.Null(FuzzyMatcher.Match([], "water lily"));
    }

    [Fact]
    public void Match_EmptyText_ReturnsNull()
    {
        Assert.Null(FuzzyMatcher.Match(["water"], ""));
    }

    [Fact]
    public void Match_CyrillicTypoInPrefix_Scores1()
    {
        var match = FuzzyMatcher.Match(["водопо"], "водопад и река");

        Assert.Equal(1, match!.Value.Score);
        Assert.Equal(0, match.Value.FirstIndex);
    }

    [Fact]
    public void Match_DigitToken_ExactPrefixWins()
    {
        var match = FuzzyMatcher.Match(["2025"], "2026 отчёт 2025");

        Assert.Equal(3, match!.Value.Score);
        Assert.Equal(11, match.Value.FirstIndex);
    }
}
