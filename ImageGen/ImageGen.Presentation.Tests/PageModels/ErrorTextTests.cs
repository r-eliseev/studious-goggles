using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Storage;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Presentation.Tests.PageModels;

public class ErrorTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static CopyTextAction Copy() =>
        new(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), "text");

    [Fact]
    public void Message_WithErrorCode_ShowsTextOfTheCode()
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Text = "provider text",
            CreatedAt = Now,
            IsError = true,
            ErrorKind = ErrorKind.RateLimited,
        };

        var item = new MessageItemViewModel(message, message.Text, Copy());

        Assert.Equal(AppResources.ErrorKindRateLimited, item.Text);
    }

    [Fact]
    public void Message_WithoutErrorCode_ShowsSavedText()
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Role = ChatRole.Assistant,
            Text = "Assistant error (Network): old text",
            CreatedAt = Now,
            IsError = true,
        };

        var item = new MessageItemViewModel(message, message.Text, Copy());

        Assert.Equal("Assistant error (Network): old text", item.Text);
    }

    [Fact]
    public async Task FailedGeneration_ShowsCodeTextAndDetailsShowProviderText()
    {
        var dialogs = Substitute.For<IDialogService>();
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProviderId = "openrouter",
            ModelId = "vendor/model",
            Prompt = "red fox",
            Seed = 1,
            SeedApplied = true,
            Status = GenerationStatus.Failed,
            ErrorKind = ErrorKind.Network,
            ErrorMessage = "dns lookup failed",
            CreatedAt = Now,
            SortOrder = 0,
        };
        var card = new GenerationItemViewModel(
            generation,
            Substitute.For<IImageFileStore>(),
            dialogs,
            Substitute.For<IClipboardService>(),
            Substitute.For<IImageSaver>(),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask);

        Assert.Equal(AppResources.ErrorKindNetwork, card.ErrorText);

        await card.DetailsCommand.ExecuteAsync(null);

        await dialogs.Received(1).AlertAsync(AppResources.ErrorDetailsTitle, "dns lookup failed");
    }
}
