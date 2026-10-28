using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public class GenerationParameterPlannerTests
{
    private const string AspectRatio = "aspect_ratio";
    private const string Resolution = "resolution";
    private const string OutputFormat = "output_format";
    private const string Quality = "quality";

    [Fact]
    public void Plan_ValueInCatalog_Applied()
    {
        var model = Catalog(Enum(AspectRatio, "1:1", "3:2", "auto"));

        var outcome = Single(Plan(new GenerationParameters { AspectRatio = "3:2" }, model));

        Assert.Equal(ParameterOutcomeKind.Applied, outcome.Kind);
        Assert.Equal("3:2", outcome.Sent);
    }

    [Fact]
    public void Plan_AspectRatioNotInCatalog_ReplacedByNearest()
    {
        var model = Catalog(Enum(AspectRatio, "1:1", "4:3", "3:4", "16:9", "9:16", "auto"));

        var outcome = Single(Plan(new GenerationParameters { AspectRatio = "3:2" }, model));

        Assert.Equal(ParameterOutcomeKind.Replaced, outcome.Kind);
        Assert.Equal("4:3", outcome.Sent);
    }

    [Fact]
    public void Plan_AspectRatio4to3_ReplacedByNearest32()
    {
        var model = Catalog(Enum(AspectRatio, "1:1", "2:3", "3:2", "auto"));

        var outcome = Single(Plan(new GenerationParameters { AspectRatio = "4:3" }, model));

        Assert.Equal("3:2", outcome.Sent);
    }

    [Fact]
    public void Plan_AspectRatioOnlyAutoInCatalog_ReplacedByAuto()
    {
        var model = Catalog(Enum(AspectRatio, "auto"));

        var outcome = Single(Plan(new GenerationParameters { AspectRatio = "4:3" }, model));

        Assert.Equal(ParameterOutcomeKind.Replaced, outcome.Kind);
        Assert.Equal("auto", outcome.Sent);
    }

    [Fact]
    public void Plan_AutoNotInCatalog_DroppedNoAllowedValue()
    {
        var model = Catalog(Enum(AspectRatio, "1:1", "3:2"));

        var outcome = Single(Plan(new GenerationParameters { AspectRatio = "auto" }, model));

        Assert.Equal(ParameterOutcomeKind.Dropped, outcome.Kind);
        Assert.Equal(ParameterDropReason.NoAllowedValue, outcome.Reason);
    }

    [Fact]
    public void Plan_AspectRatioEquidistant_FirstInCatalogWins()
    {
        var model = Catalog(Enum(AspectRatio, "1:2", "2:1"));

        var outcome = Single(Plan(new GenerationParameters { AspectRatio = "1:1" }, model));

        Assert.Equal("1:2", outcome.Sent);
    }

    [Fact]
    public void Plan_Resolution2KBetween1KAnd4K_ReplacedByNearest15K()
    {
        var model = Catalog(Enum(Resolution, "1K", "1.5K", "4K"));

        var outcome = Single(Plan(new GenerationParameters { Resolution = "2K" }, model));

        Assert.Equal("1.5K", outcome.Sent);
    }

    [Fact]
    public void Plan_Resolution4KWith1KAnd2K_ReplacedBy2K()
    {
        var model = Catalog(Enum(Resolution, "1K", "2K"));

        var outcome = Single(Plan(new GenerationParameters { Resolution = "4K" }, model));

        Assert.Equal("2K", outcome.Sent);
    }

    [Fact]
    public void Plan_OutputFormatWebpNotInCatalog_DroppedNoAllowedValue()
    {
        var model = Catalog(Enum(OutputFormat, "png", "jpeg"));

        var outcome = Single(Plan(new GenerationParameters { OutputFormat = "webp" }, model));

        Assert.Equal(ParameterOutcomeKind.Dropped, outcome.Kind);
        Assert.Equal(ParameterDropReason.NoAllowedValue, outcome.Reason);
    }

    [Fact]
    public void Plan_OutputFormatInUpperCase_AppliedWithCatalogSpelling()
    {
        var model = Catalog(Enum(OutputFormat, "png", "jpeg"));

        var outcome = Single(Plan(new GenerationParameters { OutputFormat = "PNG" }, model));

        Assert.Equal(ParameterOutcomeKind.Applied, outcome.Kind);
        Assert.Equal("png", outcome.Sent);
    }

    [Fact]
    public void Plan_ParameterNotDeclaredByModel_DroppedNotSupported()
    {
        var model = Catalog(Enum(AspectRatio, "1:1"));

        var outcome = Single(Plan(new GenerationParameters { OutputFormat = "png" }, model));

        Assert.Equal(ParameterDropReason.NotSupported, outcome.Reason);
    }

    [Fact]
    public void Plan_ModelWithoutCatalogParameters_DroppedCatalogUnknown()
    {
        var outcomes = GenerationParameterPlanner.Plan(
            new GenerationParameters { AspectRatio = "3:2", OutputFormat = "png" },
            Model(parameters: null));

        Assert.All(outcomes, o => Assert.Equal(ParameterDropReason.CatalogUnknown, o.Reason));
        Assert.Equal(2, outcomes.Count);
    }

    [Fact]
    public void Plan_ModelIsNull_DroppedCatalogUnknown()
    {
        var outcome = Single(Plan(new GenerationParameters { Quality = "high" }, null));

        Assert.Equal(ParameterDropReason.CatalogUnknown, outcome.Reason);
    }

    [Fact]
    public void Plan_NoParametersRequested_NoOutcomes()
    {
        var outcomes = GenerationParameterPlanner.Plan(GenerationParameters.Empty, Catalog(Enum(AspectRatio, "1:1")));

        Assert.Empty(outcomes);
    }

    [Fact]
    public void Plan_ModelDeclaresNoParameters_DroppedNotSupported()
    {
        var outcome = Single(Plan(new GenerationParameters { OutputFormat = "png" }, Model(parameters: [])));

        Assert.Equal(ParameterDropReason.NotSupported, outcome.Reason);
    }

    [Fact]
    public void Plan_QualityNotInCatalog_DroppedNoAllowedValue()
    {
        var model = Catalog(Enum(Quality, "low", "medium"));

        var outcome = Single(Plan(new GenerationParameters { Quality = "high" }, model));

        Assert.Equal(ParameterDropReason.NoAllowedValue, outcome.Reason);
    }

    private static IReadOnlyList<ParameterOutcome> Plan(GenerationParameters requested, ImageModel? model) =>
        GenerationParameterPlanner.Plan(requested, model);

    private static ParameterOutcome Single(IReadOnlyList<ParameterOutcome> outcomes) => Assert.Single(outcomes);

    private static ModelParameter Enum(string name, params string[] values) =>
        new(name, ParameterKind.Enum, values);

    private static ImageModel Catalog(params ModelParameter[] parameters) => Model(parameters);

    private static ImageModel Model(IReadOnlyList<ModelParameter>? parameters) => new()
    {
        ProviderId = "openrouter",
        Id = "vendor/model",
        Name = "Model",
        SupportsSeed = true,
        SupportsImageInput = false,
        MaxImagesPerRequest = 1,
        FetchedAt = DateTimeOffset.UnixEpoch,
        Parameters = parameters,
    };
}
