using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
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
    public async Task NarrowChat_LongTitle_AllChipsInside()
    {
        using var shell = await OpenChatAsync(360, new string('W', Session.MaxTitleLength));
        var chat = Assert.IsType<ChatView>(shell.Window.PageArea.Content);
        var model = Assert.IsType<ChatPageModel>(chat.DataContext);
        var header = chat.FindControl<WrapPanel>("ChatHeader")!;
        var chips = header.Children.Where(control => control is Button button
            && (AutomationProperties.GetName(button) == model.ModelsButtonText
                || AutomationProperties.GetName(button) == AppResources.ChatParametersButton)
            || control is Border border && border.Child is TextBlock text
                && AutomationProperties.GetName(text) == model.AssistantText).ToArray();

        Assert.Equal(3, chips.Length);
        foreach (var chip in chips)
        {
            Assert.True(chip.IsEffectivelyVisible);
            AssertInsideChat(chip, chat);
        }
    }

    [AvaloniaFact]
    public async Task NarrowChat_LongAssistant_AllChipsInside()
    {
        using var shell = await OpenChatAsync(360);
        var chat = Assert.IsType<ChatView>(shell.Window.PageArea.Content);
        var model = Assert.IsType<ChatPageModel>(chat.DataContext);
        var header = chat.FindControl<WrapPanel>("ChatHeader")!;
        var assistant = header.Children.OfType<Border>().Single();

        Assert.True(model.AssistantText.Length > 20);
        Assert.True(assistant.IsEffectivelyVisible);
        AssertInsideChat(assistant, chat);
    }

    [AvaloniaFact]
    public async Task NarrowChat_LongTitle_TitleTrimmed()
    {
        using var shell = await OpenChatAsync(360, new string('W', Session.MaxTitleLength));
        var chat = Assert.IsType<ChatView>(shell.Window.PageArea.Content);
        var header = chat.FindControl<WrapPanel>("ChatHeader")!;
        var title = chat.FindControl<TextBlock>("ChatTitle")!;

        Assert.True(title.IsEffectivelyVisible);
        Assert.True(title.Bounds.Width <= header.Bounds.Width);
    }

    [AvaloniaFact]
    public async Task WideChat_ShortTitle_HeaderInOneRow()
    {
        using var shell = await OpenChatAsync(1100, "Fox");
        var chat = Assert.IsType<ChatView>(shell.Window.PageArea.Content);
        var header = chat.FindControl<WrapPanel>("ChatHeader")!;
        var title = chat.FindControl<TextBlock>("ChatTitle")!;
        var chips = header.Children.Where(control => control is Button || control is Border).ToArray();
        var titleCenter = title.TranslatePoint(new Point(0, title.Bounds.Height / 2), chat)!.Value.Y;

        Assert.Equal(3, chips.Length);
        foreach (var chip in chips)
        {
            var center = chip.TranslatePoint(new Point(0, chip.Bounds.Height / 2), chat)!.Value.Y;
            var left = chip.TranslatePoint(default, chat)!.Value.X;
            Assert.InRange(Math.Abs(center - titleCenter), 0, 1);
            Assert.True(left >= title.TranslatePoint(new Point(title.Bounds.Width, 0), chat)!.Value.X);
        }
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

    private static async Task<ShellHarness> OpenChatAsync(double width, string? title = null)
    {
        var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Width = width;
        shell.Window.Show();
        await shell.Navigation.GoToAsync(Routes.Sessions);
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(TestContext.Current.CancellationToken);
        if (title is not null)
        {
            await shell.Provider.GetRequiredService<SessionService>().RenameAsync(session.Id, title, TestContext.Current.CancellationToken);
        }

        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        var model = (ChatPageModel)((ChatView)shell.Window.PageArea.Content!).DataContext!;
        await shell.WaitUntilAsync(() => model.Title == (title ?? model.Title) && model.ModelsButtonText.Length > 0, "шапка загружена");
        shell.Window.UpdateLayout();
        return shell;
    }

    private static void AssertInsideChat(Control control, Control chat)
    {
        var topLeft = control.TranslatePoint(default, chat)!.Value;
        var bottomRight = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), chat)!.Value;
        Assert.True(topLeft.X >= 0 && topLeft.Y >= 0);
        Assert.True(bottomRight.X <= chat.Bounds.Width + 0.5 && bottomRight.Y <= chat.Bounds.Height + 0.5);
    }
}
