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
}
