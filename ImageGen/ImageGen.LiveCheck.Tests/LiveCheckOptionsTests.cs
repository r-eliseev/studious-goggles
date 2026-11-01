using ImageGen.LiveCheck;

namespace ImageGen.LiveCheck.Tests;

public sealed class LiveCheckOptionsTests
{
    [Fact]
    public void Parse_ArgumentAndEnvironmentKeys_UsesArgumentsFirstAndDefaults()
    {
        var options = LiveCheckOptions.Parse(["--openrouter-key", "arg-key"], new Dictionary<string, string>
        {
            ["IMAGEGEN_LIVE_OPENROUTER_KEY"] = "env-key",
            ["IMAGEGEN_LIVE_FAL_KEY"] = "fal-env",
        });

        Assert.Equal("arg-key", options.Keys["openrouter"]);
        Assert.Equal("fal-env", options.Keys["fal"]);
        Assert.Equal(1m, options.BudgetUsd);
        Assert.Equal(40, options.MaxRequests);
    }

    [Fact]
    public void Parse_InvalidBudget_ThrowsWithoutEchoingArgumentValues()
    {
        var error = Assert.Throws<ArgumentException>(() => LiveCheckOptions.Parse(["--openrouter-key", "do-not-leak", "--budget-usd", "0"]));

        Assert.DoesNotContain("do-not-leak", error.Message, StringComparison.Ordinal);
    }
}
