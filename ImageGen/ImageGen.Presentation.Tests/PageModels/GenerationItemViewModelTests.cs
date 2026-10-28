using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.Tests.PageModels;

public class GenerationItemViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private const string Model = "black-forest-labs/flux.2-klein-4b";

    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();

    public GenerationItemViewModelTests()
    {
        _files.GetFullPath(Arg.Any<string>()).Returns(ci => "/data/" + ci.Arg<string>());
    }

    private static Generation Make(
        GenerationStatus status = GenerationStatus.Queued,
        long seed = 42,
        bool seedApplied = true,
        decimal? cost = null,
        string? imagePath = null,
        string? error = null) => new()
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProviderId = "openrouter",
            ModelId = Model,
            Prompt = "red fox",
            Seed = seed,
            SeedApplied = seedApplied,
            Status = status,
            ImagePath = imagePath,
            CostUsd = cost,
            ErrorMessage = error,
            CreatedAt = Now,
            SortOrder = 0,
        };

    private GenerationItemViewModel Create(
        Generation generation,
        Func<GenerationItemViewModel, Task>? repeat = null,
        Func<GenerationItemViewModel, Task>? newSeed = null,
        Func<GenerationItemViewModel, Task>? edit = null) =>
        new(generation, _files, repeat ?? (_ => Task.CompletedTask), newSeed ?? (_ => Task.CompletedTask), edit ?? (_ => Task.CompletedTask));

    [Fact]
    public void Queued_ShowsQueuedStatusAndIsRunning()
    {
        var card = Create(Make(GenerationStatus.Queued));

        Assert.Equal(AppResources.StatusQueued, card.StatusText);
        Assert.True(card.IsRunning);
        Assert.False(card.IsFinished);
        Assert.False(card.HasImage);
        Assert.False(card.HasErrorText);
    }

    [Fact]
    public void Running_ShowsGeneratingStatusAndIsRunning()
    {
        var card = Create(Make(GenerationStatus.Running));

        Assert.Equal(AppResources.StatusRunning, card.StatusText);
        Assert.True(card.IsRunning);
        Assert.False(card.IsFinished);
    }

    [Fact]
    public void Succeeded_ShowsDoneStatusAndImageFromFileStore()
    {
        var card = Create(Make(GenerationStatus.Succeeded, imagePath: "images/s/g.png"));

        Assert.Equal(AppResources.StatusSucceeded, card.StatusText);
        Assert.False(card.IsRunning);
        Assert.True(card.IsFinished);
        Assert.True(card.HasImage);
        Assert.Equal("/data/images/s/g.png", card.ImageFilePath);
        _files.Received(1).GetFullPath("images/s/g.png");
    }

    [Fact]
    public void Succeeded_WithoutImagePath_HasNoImage()
    {
        var card = Create(Make(GenerationStatus.Succeeded, imagePath: null));

        Assert.False(card.HasImage);
        Assert.Equal("", card.ImageFilePath);
    }

    [Fact]
    public void Failed_ShowsErrorStatusAndErrorTextAndHasError()
    {
        var card = Create(Make(GenerationStatus.Failed, error: "Model is overloaded"));

        Assert.Equal(AppResources.StatusFailed, card.StatusText);
        Assert.Equal("Model is overloaded", card.ErrorText);
        Assert.True(card.HasErrorText);
        Assert.True(card.HasError);
        Assert.True(card.IsFinished);
    }

    [Fact]
    public void Filtered_ShowsFilteredStatusWithReasonButIsNotAnError()
    {
        var card = Create(Make(GenerationStatus.Filtered, error: "Moderated: violence"));

        Assert.Equal(AppResources.StatusFiltered, card.StatusText);
        Assert.Equal("Moderated: violence", card.ErrorText);
        Assert.True(card.HasErrorText);
        Assert.False(card.HasError);
        Assert.True(card.IsFinished);
    }

    [Fact]
    public void Cancelled_ShowsCancelledStatus()
    {
        var card = Create(Make(GenerationStatus.Cancelled));

        Assert.Equal(AppResources.StatusCancelled, card.StatusText);
        Assert.True(card.IsFinished);
    }

    [Fact]
    public void Details_SeedAppliedAndCost_ShowsModelSeedAndPrice()
    {
        var card = Create(Make(GenerationStatus.Succeeded, seed: 42, cost: 0.014m, imagePath: "p.png"));

        Assert.Equal("black-forest-labs/flux.2-klein-4b · seed 42 · $0.014", card.DetailsText);
    }

    [Fact]
    public void Details_SeedNotApplied_ShowsSeedNotSupportedAndNoPriceWhenUnknown()
    {
        var card = Create(Make(GenerationStatus.Failed, seedApplied: false, cost: null));

        Assert.Equal("black-forest-labs/flux.2-klein-4b · seed not supported", card.DetailsText);
    }

    [Fact]
    public void Details_LongCost_RoundsToFourDecimals()
    {
        var card = Create(Make(GenerationStatus.Succeeded, cost: 0.123456m, imagePath: "p.png"));

        Assert.EndsWith(" · $0.1235", card.DetailsText);
    }

    [Fact]
    public void Update_QueuedToSucceeded_RefreshesStatusImageAndCommands()
    {
        var generation = Make(GenerationStatus.Queued);
        var card = Create(generation);
        Assert.False(card.RepeatCommand.CanExecute(null));

        card.Update(new Generation
        {
            Id = generation.Id,
            RunId = generation.RunId,
            SessionId = generation.SessionId,
            ProviderId = generation.ProviderId,
            ModelId = generation.ModelId,
            Prompt = generation.Prompt,
            Seed = generation.Seed,
            SeedApplied = true,
            Status = GenerationStatus.Succeeded,
            ImagePath = "images/s/g.png",
            CostUsd = 0.014m,
            CreatedAt = Now,
            SortOrder = 0,
        });

        Assert.Equal(AppResources.StatusSucceeded, card.StatusText);
        Assert.True(card.HasImage);
        Assert.False(card.IsRunning);
        Assert.True(card.RepeatCommand.CanExecute(null));
    }

    [Fact]
    public void Update_OtherGeneration_Throws()
    {
        var card = Create(Make());

        Assert.Throws<ArgumentException>(() => card.Update(Make(GenerationStatus.Succeeded)));
    }

    [Fact]
    public void Commands_NotFinished_CannotExecute()
    {
        var card = Create(Make(GenerationStatus.Running));

        Assert.False(card.RepeatCommand.CanExecute(null));
        Assert.False(card.NewSeedCommand.CanExecute(null));
        Assert.False(card.EditCommand.CanExecute(null));
    }

    [Fact]
    public void Commands_Finished_CanExecute()
    {
        var card = Create(Make(GenerationStatus.Failed));

        Assert.True(card.RepeatCommand.CanExecute(null));
        Assert.True(card.NewSeedCommand.CanExecute(null));
        Assert.True(card.EditCommand.CanExecute(null));
    }

    [Fact]
    public async Task RepeatCommand_CallsRepeatWithThisCard()
    {
        GenerationItemViewModel? called = null;
        var card = Create(Make(GenerationStatus.Succeeded), repeat: target => { called = target; return Task.CompletedTask; });

        await card.RepeatCommand.ExecuteAsync(null);

        Assert.Same(card, called);
    }

    [Fact]
    public async Task NewSeedCommand_CallsNewSeedWithThisCard()
    {
        GenerationItemViewModel? called = null;
        var card = Create(Make(GenerationStatus.Succeeded), newSeed: target => { called = target; return Task.CompletedTask; });

        await card.NewSeedCommand.ExecuteAsync(null);

        Assert.Same(card, called);
    }

    [Fact]
    public async Task EditCommand_CallsEditWithThisCard()
    {
        GenerationItemViewModel? called = null;
        var card = Create(Make(GenerationStatus.Succeeded), edit: target => { called = target; return Task.CompletedTask; });

        await card.EditCommand.ExecuteAsync(null);

        Assert.Same(card, called);
    }

    [Fact]
    public void Prompt_IsTheExactPromptOfTheGeneration()
    {
        var card = Create(Make());

        Assert.Equal("red fox", card.Prompt);
    }

    [Fact]
    public void SummaryText_JoinsStatusDetailsAndError()
    {
        var card = Create(Make(GenerationStatus.Failed, seed: 42, error: "boom"));

        Assert.Equal("Error. black-forest-labs/flux.2-klein-4b · seed 42. boom", card.SummaryText);
    }
}
