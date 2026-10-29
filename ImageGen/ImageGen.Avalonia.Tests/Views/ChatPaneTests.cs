using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation.PageModels;

namespace ImageGen.Avalonia.Tests.Views;

/// <summary>
/// The models and parameters side pane on a wide window (task 071): the Models button opens a 360 pane beside the
/// chat, a narrow window navigates to the screen as before, and the pane's Save closes it.
/// </summary>
public class ChatPaneTests
{
    [AvaloniaFact]
    public async Task Wide_ModelsButton_ShowsPane()
    {
        using var shell = new ShellHarness();
        var chat = await OpenWideChatAsync(shell);

        await chat.OpenModelsCommand.ExecuteAsync(null);
        shell.Window.UpdateLayout();

        Assert.IsType<ChatView>(shell.Window.PageArea.Content);
        Assert.NotNull(chat.ModelsPane);
        var pane = SidePane(shell);
        Assert.True(pane.IsVisible);
        Assert.Single(pane.GetVisualDescendants().OfType<SessionModelsView>());
    }

    [AvaloniaFact]
    public async Task Narrow_ModelsButton_Navigates()
    {
        using var shell = new ShellHarness();
        shell.Window.Width = 600;
        shell.Window.Show();
        var chat = await shell.OpenNewChatAsync();
        shell.Window.UpdateLayout();
        Assert.False(chat.IsWide);

        await chat.OpenModelsCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionModelsView, "экран моделей");

        Assert.IsType<SessionModelsView>(shell.Window.PageArea.Content);
        Assert.Null(chat.ModelsPane);
    }

    [AvaloniaFact]
    public async Task Pane_Save_Closes()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await OpenWideChatAsync(shell);
        await chat.OpenModelsCommand.ExecuteAsync(null);
        var pane = chat.ModelsPane!;
        await shell.WaitUntilAsync(() => !pane.IsBusy, "панель загружена");

        await pane.SaveCommand.ExecuteAsync(null);
        shell.Window.UpdateLayout();

        Assert.Null(chat.ModelsPane);
        Assert.False(SidePane(shell).IsVisible);
    }

    private static Border SidePane(ShellHarness shell) =>
        ((Control)shell.Window.PageArea.Content!).GetVisualDescendants().OfType<Border>()
            .First(border => border.Name == "SidePane");

    private static async Task<ChatPageModel> OpenWideChatAsync(ShellHarness shell)
    {
        shell.Window.Width = 1100;
        shell.Window.Show();
        var chat = await shell.OpenNewChatAsync();
        shell.Window.UpdateLayout();
        return chat;
    }
}
