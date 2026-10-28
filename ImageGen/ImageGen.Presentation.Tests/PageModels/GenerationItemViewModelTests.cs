using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Storage;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.Tests.PageModels;

public class GenerationItemViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Details_ReplacedParameter_ShowsRequestedAndSentValues()
    {
        var card = Card(new ParameterOutcome("aspect_ratio", ParameterOutcomeKind.Replaced, "3:2", "4:3"));

        Assert.Contains("3:2 → 4:3", card.DetailsText, StringComparison.Ordinal);
    }

    [Fact]
    public void Details_DroppedParameter_ShowsReason()
    {
        var card = Card(new ParameterOutcome("output_format", ParameterOutcomeKind.Dropped, "webp", null, ParameterDropReason.NotSupported));

        Assert.Contains("webp: not sent (the model does not support it)", card.DetailsText, StringComparison.Ordinal);
        Assert.Contains(card.DetailsText, card.SummaryText);
    }

    [Fact]
    public void Details_AppliedParameter_AddsNoLine()
    {
        var card = Card(new ParameterOutcome("quality", ParameterOutcomeKind.Applied, "high", "high"));

        Assert.DoesNotContain(AppResources.ParameterQuality, card.DetailsText, StringComparison.Ordinal);
    }

    [Fact]
    public void Details_NoOutcomes_TextUnchanged()
    {
        var card = Card();

        Assert.DoesNotContain("→", card.DetailsText, StringComparison.Ordinal);
    }

    private static GenerationItemViewModel Card(params ParameterOutcome[] outcomes) => new(
        new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProviderId = "openrouter",
            ModelId = "vendor/a",
            Prompt = "fox",
            Seed = 1,
            SeedApplied = true,
            Status = GenerationStatus.Succeeded,
            CreatedAt = Now,
            SortOrder = 0,
            Outcomes = outcomes,
        },
        Substitute.For<IImageFileStore>(),
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask);
}
