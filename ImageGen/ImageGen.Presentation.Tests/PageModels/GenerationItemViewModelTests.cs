using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Storage;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Tests.PageModels;

public class GenerationItemViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShowActions_ChoosesRepeat_RunsRepeat()
    {
        var repeated = false;
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(AppResources.Repeat);
        var card = CardWithActions(dialogs, repeat: _ => { repeated = true; return Task.CompletedTask; });

        await card.ShowActionsCommand.ExecuteAsync(null);

        Assert.True(repeated);
    }

    [Fact]
    public async Task ShowActions_Cancel_DoesNothing()
    {
        var repeated = false;
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns((string?)null);
        var card = CardWithActions(dialogs, repeat: _ => { repeated = true; return Task.CompletedTask; });

        await card.ShowActionsCommand.ExecuteAsync(null);

        Assert.False(repeated);
    }

    [Fact]
    public void ShowActions_NotFinished_CannotExecute()
    {
        var generation = BaseGeneration();
        generation.Status = GenerationStatus.Running;

        var card = CardOf(generation);

        Assert.False(card.ShowActionsCommand.CanExecute(null));
    }

    [Fact]
    public void Details_OnlyForFailed()
    {
        var failed = BaseGeneration();
        failed.Status = GenerationStatus.Failed;
        failed.ErrorMessage = "boom";
        var filtered = BaseGeneration();
        filtered.Status = GenerationStatus.Filtered;
        var succeeded = BaseGeneration();
        succeeded.Status = GenerationStatus.Succeeded;

        Assert.True(CardOf(failed).DetailsCommand.CanExecute(null));
        Assert.False(CardOf(filtered).DetailsCommand.CanExecute(null));
        Assert.False(CardOf(succeeded).DetailsCommand.CanExecute(null));
    }

    [Fact]
    public async Task Details_ShowsErrorText()
    {
        var generation = BaseGeneration();
        generation.Status = GenerationStatus.Failed;
        generation.ErrorMessage = "the provider said no";
        var dialogs = Substitute.For<IDialogService>();
        var card = CardOf(generation, dialogs);

        await card.DetailsCommand.ExecuteAsync(null);

        await dialogs.Received(1).AlertAsync(AppResources.ErrorDetailsTitle, "the provider said no");
    }

    [Fact]
    public void OtherModel_ForFailedAndFiltered()
    {
        var failed = BaseGeneration();
        failed.Status = GenerationStatus.Failed;
        var filtered = BaseGeneration();
        filtered.Status = GenerationStatus.Filtered;
        var succeeded = BaseGeneration();
        succeeded.Status = GenerationStatus.Succeeded;

        Assert.True(CardOf(failed).OtherModelCommand.CanExecute(null));
        Assert.True(CardOf(filtered).OtherModelCommand.CanExecute(null));
        Assert.False(CardOf(succeeded).OtherModelCommand.CanExecute(null));
    }

    [Fact]
    public async Task CopyPrompt_CopiesExactPrompt()
    {
        var generation = BaseGeneration();
        generation.Status = GenerationStatus.Succeeded;
        generation.Prompt = "a red fox in snow";
        var clipboard = Substitute.For<IClipboardService>();
        var card = CardOf(generation, clipboard: clipboard);

        await card.CopyPromptCommand.ExecuteAsync(null);

        await clipboard.Received(1).SetTextAsync("a red fox in snow");
    }

    private static GenerationItemViewModel CardWithActions(
        IDialogService dialogs,
        Func<GenerationItemViewModel, Task>? repeat = null) => new(
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
        },
        Substitute.For<IImageFileStore>(),
        dialogs,
        Substitute.For<IClipboardService>(),
        repeat ?? (_ => Task.CompletedTask),
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        (_, _) => Task.CompletedTask,
        _ => Task.CompletedTask);

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
    public void Details_ReferenceImages_ShowsTheirCount()
    {
        var card = Card(referenceCount: 2);

        Assert.Contains("Reference images: 2", card.DetailsText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DescribeCommand_CallsDescribeWithDescriptionMode()
    {
        DescriptionMode? requested = null;
        var card = CardWithDescribe((_, mode) =>
        {
            requested = mode;
            return Task.CompletedTask;
        });

        await card.DescribeCommand.ExecuteAsync(null);

        Assert.Equal(DescriptionMode.Description, requested);
    }

    [Fact]
    public async Task DescribePromptCommand_CallsDescribeWithPromptMode()
    {
        DescriptionMode? requested = null;
        var card = CardWithDescribe((_, mode) =>
        {
            requested = mode;
            return Task.CompletedTask;
        });

        await card.DescribePromptCommand.ExecuteAsync(null);

        Assert.Equal(DescriptionMode.Prompt, requested);
    }

    private static GenerationItemViewModel CardWithDescribe(Func<GenerationItemViewModel, DescriptionMode, Task> describe) => new(
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
        },
        Substitute.For<IImageFileStore>(),
        Substitute.For<IDialogService>(),
        Substitute.For<IClipboardService>(),
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        describe,
        _ => Task.CompletedTask);

    [Fact]
    public void Details_NoOutcomes_TextUnchanged()
    {
        var card = Card();

        Assert.DoesNotContain("→", card.DetailsText, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_Succeeded_FillsModelSeedCostTime()
    {
        var generation = BaseGeneration();
        generation.Status = GenerationStatus.Succeeded;
        generation.ModelId = "vendor/model";
        generation.Seed = 42;
        generation.SeedApplied = true;
        generation.CostUsd = 0.014m;
        generation.CompletedAt = generation.CreatedAt.AddSeconds(3);

        var card = CardOf(generation);

        Assert.Equal(GenerationStatusKind.Done, card.StatusKind);
        Assert.Equal("vendor/model", card.ModelText);
        Assert.Equal(string.Format(AppResources.SeedFormat, 42), card.SeedText);
        Assert.Equal(string.Format(AppResources.CostFormat, "0.014"), card.CostText);
        Assert.Equal(string.Format(AppResources.TimeFormat, "3"), card.TimeText);
    }

    [Fact]
    public void Update_Filtered_SetsFilteredKind()
    {
        var generation = BaseGeneration();
        generation.Status = GenerationStatus.Filtered;

        var card = CardOf(generation);

        Assert.Equal(GenerationStatusKind.Filtered, card.StatusKind);
        Assert.True(card.IsFiltered);
        Assert.False(card.IsQueued);
        Assert.False(card.IsCancelled);
        Assert.Equal(AppResources.FilteredNotice, card.NoticeText);
    }

    [Fact]
    public void Update_SeedNotApplied_ShowsSeedNotSupported()
    {
        var generation = BaseGeneration();
        generation.Status = GenerationStatus.Succeeded;
        generation.SeedApplied = false;

        var card = CardOf(generation);

        Assert.Equal(AppResources.SeedNotSupported, card.SeedText);
    }

    [Fact]
    public void Update_DroppedParameter_FillsNotice()
    {
        var generation = BaseGeneration();
        generation.Status = GenerationStatus.Succeeded;
        generation.Outcomes = [new ParameterOutcome("output_format", ParameterOutcomeKind.Dropped, "webp", null, ParameterDropReason.NotSupported)];

        var card = CardOf(generation);

        Assert.Contains("webp: not sent (the model does not support it)", card.NoticeText, StringComparison.Ordinal);
    }

    private static Generation BaseGeneration() => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        ProviderId = "openrouter",
        ModelId = "vendor/model",
        Prompt = "fox",
        Seed = 1,
        SeedApplied = true,
        Status = GenerationStatus.Succeeded,
        CreatedAt = Now,
        SortOrder = 0,
    };

    private static GenerationItemViewModel CardOf(
        Generation generation,
        IDialogService? dialogs = null,
        IClipboardService? clipboard = null) => new(
        generation,
        Substitute.For<IImageFileStore>(),
        dialogs ?? Substitute.For<IDialogService>(),
        clipboard ?? Substitute.For<IClipboardService>(),
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        (_, _) => Task.CompletedTask,
        _ => Task.CompletedTask);

    private static GenerationItemViewModel Card(params ParameterOutcome[] outcomes) => Card(outcomes, 0);

    private static GenerationItemViewModel Card(int referenceCount) => Card([], referenceCount);

    private static GenerationItemViewModel Card(ParameterOutcome[] outcomes, int referenceCount) => new(
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
            ReferenceAttachmentIds = Enumerable.Range(0, referenceCount).Select(_ => Guid.NewGuid()).ToList(),
        },
        Substitute.For<IImageFileStore>(),
        Substitute.For<IDialogService>(),
        Substitute.For<IClipboardService>(),
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        (_, _) => Task.CompletedTask,
        _ => Task.CompletedTask);
}
