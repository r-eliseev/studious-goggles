using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Presentation.Tests.PageModels;

public class GenerationSaveTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IImageSaver _saver = Substitute.For<IImageSaver>();

    private GenerationItemViewModel MakeCard(GenerationStatus status, string? imagePath = "images/fox.png")
    {
        var generation = new Generation
        {
            Id = Guid.Parse("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0"),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProviderId = "openrouter",
            ModelId = "vendor/model",
            Prompt = "red fox",
            Seed = 1,
            SeedApplied = true,
            Status = status,
            ImagePath = imagePath,
            MediaType = "image/png",
            CreatedAt = Now,
            SortOrder = 0,
        };
        var files = Substitute.For<IImageFileStore>();
        files.GetFullPath("images/fox.png").Returns("/data/images/fox.png");
        return new GenerationItemViewModel(
            generation, files, _dialogs, Substitute.For<IClipboardService>(), _saver,
            _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask, (_, _) => Task.CompletedTask, _ => Task.CompletedTask);
    }

    [Fact]
    public async Task Save_CallsSaverWithSuggestedNameAndMediaType()
    {
        var card = MakeCard(GenerationStatus.Succeeded);
        _saver.SaveAsync("/data/images/fox.png", "imagegen-0f1e2d3c.png", "image/png", Arg.Any<CancellationToken>())
            .Returns(ImageSaveResult.Saved);

        await card.SaveCommand.ExecuteAsync(null);

        await _saver.Received(1).SaveAsync("/data/images/fox.png", "imagegen-0f1e2d3c.png", "image/png", Arg.Any<CancellationToken>());
        Assert.Equal(AppResources.Saved, card.SaveStatusText);
    }

    [Fact]
    public async Task Save_PermissionDenied_ShowsMessageAndNoStatus()
    {
        var card = MakeCard(GenerationStatus.Succeeded);
        _saver.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ImageSaveResult.PermissionDenied);

        await card.SaveCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.SavePermissionDenied);
        Assert.Equal("", card.SaveStatusText);
    }

    [Fact]
    public async Task Save_Cancelled_ShowsNothing()
    {
        var card = MakeCard(GenerationStatus.Succeeded);
        _saver.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ImageSaveResult.Cancelled);

        await card.SaveCommand.ExecuteAsync(null);

        await _dialogs.DidNotReceive().AlertAsync(Arg.Any<string>(), Arg.Any<string>());
        Assert.Equal("", card.SaveStatusText);
    }

    [Fact]
    public async Task Save_Failed_ShowsErrorMessage()
    {
        var card = MakeCard(GenerationStatus.Succeeded);
        _saver.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ImageSaveResult.Failed);

        await card.SaveCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.SaveFailed);
    }

    [Fact]
    public void Save_NoImage_CannotExecute()
    {
        var card = MakeCard(GenerationStatus.Queued, imagePath: null);

        Assert.False(card.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void SaveLabel_ComesFromTheShell()
    {
        _saver.ActionLabel.Returns("Save to Photos");
        var card = MakeCard(GenerationStatus.Succeeded);

        Assert.Equal("Save to Photos", card.SaveLabel);
    }
}
