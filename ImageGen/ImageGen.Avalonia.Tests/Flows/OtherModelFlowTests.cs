using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Services;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Flows;

public class OtherModelFlowTests
{
    [AvaloniaFact]
    public async Task FilteredCard_OtherModel_StartsRunOnPickedModel()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create(imagesFiltered: true));
        var chat = await shell.OpenNewChatAsync();
        var row = await SendAsync(shell, chat, "draw a red fox");
        var card = row.Generations.Single();
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();
        await shell.WaitUntilAsync(() => card.IsFiltered, "card filtered");

        await card.OtherModelCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ModelPickerView, "model picker screen");
        var picker = (ModelPickerPageModel)((Control)shell.Window.PageArea.Content!).DataContext!;
        await shell.WaitUntilAsync(() => picker.AllModels.Count == 6, "catalog loaded");

        var chosen = picker.AllModels.Single(m => m.Id == "vendor/beta");
        await picker.PickCommand.ExecuteAsync(chosen);
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "returned to chat");
        await shell.WaitUntilAsync(() => chat.Items.OfType<RunItemViewModel>().Count() == 2, "second run in transcript");

        var runs = chat.Items.OfType<RunItemViewModel>().ToList();
        Assert.Equal("vendor/beta", runs[1].Generations.Single().ModelText);
    }

    private static async Task<RunItemViewModel> SendAsync(ShellHarness shell, ChatPageModel chat, string text)
    {
        chat.InputText = text;
        await chat.SendCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => chat.Items.OfType<RunItemViewModel>().Any(), "run in transcript");
        return chat.Items.OfType<RunItemViewModel>().First();
    }
}
