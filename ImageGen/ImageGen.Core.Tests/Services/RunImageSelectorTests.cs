using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Services;

namespace ImageGen.Core.Tests.Services;

public class RunImageSelectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Select_NoGenerations_ReturnsEmpty()
    {
        Assert.Empty(RunImageSelector.Select([]));
    }

    [Fact]
    public void Select_MoreThanLimit_ReturnsFirstBySortOrder()
    {
        var generations = Enumerable.Range(0, 6).Select(i => Succeeded(sortOrder: 5 - i)).ToList();

        var chosen = RunImageSelector.Select(generations);

        Assert.Equal(AssistantPrompts.MaxRunImages, chosen.Count);
        Assert.Equal([0, 1, 2, 3], chosen.Select(g => g.SortOrder));
    }

    [Theory]
    [InlineData(GenerationStatus.Failed)]
    [InlineData(GenerationStatus.Filtered)]
    [InlineData(GenerationStatus.Queued)]
    [InlineData(GenerationStatus.Running)]
    public void Select_NotSucceeded_Skipped(GenerationStatus status)
    {
        var chosen = RunImageSelector.Select([Succeeded(0), Generation(status, 1, "images/x.png")]);

        Assert.Equal([0], chosen.Select(g => g.SortOrder));
    }

    [Fact]
    public void Select_SucceededWithoutImagePath_Skipped()
    {
        var chosen = RunImageSelector.Select([Generation(GenerationStatus.Succeeded, 0, null), Succeeded(1)]);

        Assert.Equal([1], chosen.Select(g => g.SortOrder));
    }

    private static Generation Succeeded(int sortOrder) =>
        Generation(GenerationStatus.Succeeded, sortOrder, $"images/{sortOrder}.png");

    private static Generation Generation(GenerationStatus status, int sortOrder, string? imagePath) => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        ProviderId = "openrouter",
        ModelId = "vendor/a",
        Prompt = "fox",
        Seed = 1,
        SeedApplied = true,
        Status = status,
        ImagePath = imagePath,
        MediaType = "image/png",
        CreatedAt = Now,
        SortOrder = sortOrder,
    };
}
