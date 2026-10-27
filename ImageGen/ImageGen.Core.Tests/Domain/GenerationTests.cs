using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Tests.Domain;

public class GenerationTests
{
    [Theory]
    [InlineData(GenerationStatus.Queued, false)]
    [InlineData(GenerationStatus.Running, false)]
    [InlineData(GenerationStatus.Succeeded, true)]
    [InlineData(GenerationStatus.Failed, true)]
    [InlineData(GenerationStatus.Cancelled, true)]
    [InlineData(GenerationStatus.Filtered, true)]
    public void IsFinished_ForEachStatus_ReturnsExpected(GenerationStatus status, bool expected)
    {
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ModelId = "m",
            Prompt = "p",
            Seed = 1,
            SeedApplied = true,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            SortOrder = 0,
        };

        Assert.Equal(expected, generation.IsFinished);
    }
}
