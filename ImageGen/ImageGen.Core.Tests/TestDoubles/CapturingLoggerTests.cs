using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Tests.TestDoubles;

public class CapturingLoggerTests
{
    [Fact]
    public void Default_KeepsDebug()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        logger.LogDebug("debug message");

        Assert.Equal(LogLevel.Debug, Assert.Single(logger.Entries).Level);
        Assert.Contains("debug message", logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public void MinimumInformation_ExcludesDebug()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>(LogLevel.Information);

        logger.LogDebug("debug message");
        logger.LogInformation("information message");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.DoesNotContain("debug message", logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public void State_ContainsTemplateProperties()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        logger.LogWarning("images removed {ImagesRemoved} of {ImagesKept}", 2, 4);

        var state = Assert.Single(logger.Entries).State;
        Assert.Equal(2, state["ImagesRemoved"]);
        Assert.Equal(4, state["ImagesKept"]);
    }

    [Fact]
    public void State_IsEmpty_WhenNothingWasLogged()
    {
        var logger = new CapturingLogger<CapturingLoggerTests>();

        logger.LogWarning("no properties here");

        Assert.Empty(Assert.Single(logger.Entries).State);
    }
}
