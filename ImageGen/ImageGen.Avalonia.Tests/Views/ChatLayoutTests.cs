using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Views;

public class ChatLayoutTests
{
    [AvaloniaFact]
    public async Task WideChat_ShowsSessionsList()
    {
        using var shell = await OpenChatAsync(width: 1100);

        var chat = Assert.IsType<ChatView>(shell.Window.PageArea.Content);
        Assert.True(chat.GetVisualDescendants().OfType<SessionsListView>().Single().IsVisible);
    }

    [AvaloniaFact]
    public async Task NarrowChat_HidesSessionsList()
    {
        using var shell = await OpenChatAsync(width: 600);

        var chat = Assert.IsType<ChatView>(shell.Window.PageArea.Content);
        Assert.False(chat.GetVisualDescendants().OfType<SessionsListView>().Single().IsVisible);
    }

    [AvaloniaFact]
    public async Task SelectSessionInChat_ReplacesChat_BackReturnsToList()
    {
        using var shell = await OpenChatAsync(width: 1100);
        var sessions = shell.Provider.GetRequiredService<SessionService>();
        var other = await sessions.CreateAsync(TestContext.Current.CancellationToken);
        var list = shell.Provider.GetRequiredService<SessionsPageModel>();
        await list.OnAppearingAsync();
        var row = list.Sessions.Single(item => item.Id == other.Id);

        await row.OpenCommand.ExecuteAsync(null);
        await shell.Navigation.GoBackAsync();

        Assert.IsType<SessionsView>(shell.Window.PageArea.Content);
    }

    private static async Task<ShellHarness> OpenChatAsync(double width)
    {
        var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Width = width;
        shell.Window.Show();
        await shell.Navigation.GoToAsync(Routes.Sessions);
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(TestContext.Current.CancellationToken);
        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        shell.Window.UpdateLayout();
        return shell;
    }
}
