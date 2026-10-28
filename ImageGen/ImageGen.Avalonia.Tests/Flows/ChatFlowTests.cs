using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using ImageGen.Avalonia.Converters;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.Flows;

public class ChatFlowTests
{
    [AvaloniaFact]
    public async Task SendMessage_AssistantStartsRun_CardFinishesInPlaceWithImage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await shell.OpenNewChatAsync();
        var row = await SendAsync(shell, chat, "draw a red fox");
        var card = row.Generations.Single();

        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();
        await shell.WaitUntilAsync(() => card.IsFinished && card.HasImage, "карточка готова");

        Assert.Equal(AppResources.StatusSucceeded, card.StatusText);
        Assert.Same(row, chat.Items.OfType<RunItemViewModel>().Single());
        Assert.Same(card, row.Generations.Single());
        Assert.NotNull(new FilePathToBitmapConverter()
            .Convert(card.ImageFilePath, typeof(Bitmap), null, CultureInfo.InvariantCulture));
    }

    [AvaloniaFact]
    public async Task Repeat_AfterFinish_StartsSecondRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await shell.OpenNewChatAsync();
        var row = await SendAsync(shell, chat, "draw a red fox");
        var card = row.Generations.Single();
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();
        await shell.WaitUntilAsync(() => card.IsFinished, "карточка готова");

        await card.RepeatCommand.ExecuteAsync(null);
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();

        Assert.Equal(2, chat.Items.OfType<RunItemViewModel>().Count());
    }

    [AvaloniaFact]
    public async Task NewSeed_AfterFinish_StartsSecondRunWithAnotherSeed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await shell.OpenNewChatAsync();
        var row = await SendAsync(shell, chat, "draw a red fox");
        var card = row.Generations.Single();
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();
        await shell.WaitUntilAsync(() => card.IsFinished, "карточка готова");

        await card.NewSeedCommand.ExecuteAsync(null);
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();

        var runs = chat.Items.OfType<RunItemViewModel>().ToList();
        Assert.Equal(2, runs.Count);
        Assert.Equal(runs[0].PromptText, runs[1].PromptText);
        Assert.NotEqual(runs[0].Generations.Single().DetailsText, runs[1].Generations.Single().DetailsText);
    }

    [AvaloniaFact]
    public async Task Edit_AsksForPrompt_AndStartsRunWithNewPrompt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var dialogs = Substitute.For<IDialogService>();
        dialogs.PromptAsync(default!, default!, default!, default!, default!).ReturnsForAnyArgs("a blue fox");
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create(), dialogs: dialogs);
        var chat = await shell.OpenNewChatAsync();
        var row = await SendAsync(shell, chat, "draw a red fox");
        var card = row.Generations.Single();
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();
        await shell.WaitUntilAsync(() => card.IsFinished, "карточка готова");

        await card.EditCommand.ExecuteAsync(null);
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();

        await dialogs.Received(1).PromptAsync(AppResources.EditPromptTitle, AppResources.EditPromptMessage, AppResources.Generate, AppResources.Cancel, "a red fox");
        var runs = chat.Items.OfType<RunItemViewModel>().ToList();
        Assert.Equal(2, runs.Count);
        Assert.Equal(string.Format(AppResources.RunPromptFormat, "a blue fox"), runs[1].PromptText);
    }

    [AvaloniaFact]
    public async Task Edit_Cancelled_StartsNoRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var dialogs = Substitute.For<IDialogService>();
        dialogs.PromptAsync(default!, default!, default!, default!, default!).ReturnsForAnyArgs((string?)null);
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create(), dialogs: dialogs);
        var chat = await shell.OpenNewChatAsync();
        var row = await SendAsync(shell, chat, "draw a red fox");
        var card = row.Generations.Single();
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();
        await shell.WaitUntilAsync(() => card.IsFinished, "карточка готова");

        await card.EditCommand.ExecuteAsync(null);
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();

        Assert.Single(chat.Items.OfType<RunItemViewModel>());
    }

    [AvaloniaFact]
    public async Task AssistantError_KeepsUserMessageAndShowsErrorMessage()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create(chatStatus: HttpStatusCode.TooManyRequests));
        var chat = await shell.OpenNewChatAsync();

        chat.InputText = "draw a red fox";
        await chat.SendCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => !chat.IsBusy, "ответ получен");

        var messages = chat.Items.OfType<MessageItemViewModel>().ToList();
        Assert.Contains(messages, message => !message.IsUser && message.IsError);
        Assert.Contains(messages, message => message.IsUser && message.Text == "draw a red fox");
    }

    /// <summary>Sends a message and returns the run row the assistant started (waits until the row appears).</summary>
    private static async Task<RunItemViewModel> SendAsync(ShellHarness shell, ChatPageModel chat, string text)
    {
        chat.InputText = text;
        await chat.SendCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => chat.Items.OfType<RunItemViewModel>().Any(), "прогон в ленте");
        return chat.Items.OfType<RunItemViewModel>().First();
    }
}
