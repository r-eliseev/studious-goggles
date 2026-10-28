using ImageGen.Core.Context;
using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Tests.Context;

public class TokenEstimatorTests
{
    [Fact]
    public void Estimate_EmptyText_IsOverheadOnly()
    {
        Assert.Equal(4, TokenEstimator.Estimate(""));
    }

    [Fact]
    public void Estimate_ThirtyFiveCharacters_IsTenPlusOverhead()
    {
        Assert.Equal(14, TokenEstimator.Estimate(new string('a', 35)));
    }

    [Fact]
    public void EstimateImages_CountTimesImageTokens()
    {
        Assert.Equal(3 * TokenEstimator.ImageTokens, TokenEstimator.EstimateImages(3));
    }

    [Fact]
    public void Budget_SubtractsReserveAndParts()
    {
        var budget = new ContextBudget(ContextLength: 8000, FixedTokens: 1000, ImageTokens: 1600, SummaryTokens: 0);

        Assert.Equal(8000 - TokenEstimator.ReserveForOutput - 1000 - 1600, budget.HistoryBudget);
    }

    [Fact]
    public void Budget_NeverNegative()
    {
        var budget = new ContextBudget(ContextLength: 1000, FixedTokens: 0, ImageTokens: 0, SummaryTokens: 0);

        Assert.Equal(0, budget.HistoryBudget);
    }
}
