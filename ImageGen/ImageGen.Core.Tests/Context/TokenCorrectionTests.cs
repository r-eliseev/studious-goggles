using ImageGen.Core.Context;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Context;

public class TokenCorrectionTests
{
    private static readonly ModelRef Model = new(ProviderIds.OpenRouter, "vendor/assistant");

    [Fact]
    public void FactorFor_Unknown_Is1()
    {
        Assert.Equal(1.0, new TokenCorrection().FactorFor(Model));
    }

    [Fact]
    public void Record_ClampsTo2()
    {
        var correction = new TokenCorrection();

        correction.Record(Model, estimated: 10, actual: 100);

        Assert.Equal(2.0, correction.FactorFor(Model));
    }

    [Fact]
    public void Record_ClampsTo05()
    {
        var correction = new TokenCorrection();

        correction.Record(Model, estimated: 100, actual: 1);

        Assert.Equal(0.5, correction.FactorFor(Model));
    }

    [Fact]
    public void Record_KeepsRatioInsideRange()
    {
        var correction = new TokenCorrection();

        correction.Record(Model, estimated: 100, actual: 120);

        Assert.Equal(1.2, correction.FactorFor(Model), precision: 6);
    }
}
