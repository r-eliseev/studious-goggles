using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Flows;

public class ChatKeyboardFlowTests
{
    [AvaloniaFact]
    public async Task Enter_SendsInputAndAddsNoNewLine()
    {
        var ct = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(ct);
        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "чат");
        var view = (ChatView)shell.Window.PageArea.Content!;
        var model = (ChatPageModel)view.DataContext!;
        var input = InputOf(view);
        model.InputText = "hi";

        var args = Press(input, Key.Enter, KeyModifiers.None);

        Assert.True(args.Handled);
        await shell.WaitUntilAsync(() => model.Items.Count > 0 && !model.IsBusy, "сообщение отправлено");
        Assert.Equal("", model.InputText);
        Assert.DoesNotContain('\n', input.Text ?? "");
    }

    [AvaloniaFact]
    public async Task ShiftEnter_IsNotHandled_SoTheInputKeepsItsText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(ct);
        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "чат");
        var view = (ChatView)shell.Window.PageArea.Content!;
        var model = (ChatPageModel)view.DataContext!;
        model.InputText = "hi";

        var args = Press(InputOf(view), Key.Enter, KeyModifiers.Shift);

        Assert.False(args.Handled);
        Assert.Equal("hi", model.InputText);
        Assert.Empty(model.Items);
    }

    [AvaloniaFact]
    public async Task Enter_WithEmptyInput_SendsNothingAndAddsNoNewLine()
    {
        var ct = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(ct);
        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "чат");
        var view = (ChatView)shell.Window.PageArea.Content!;
        var model = (ChatPageModel)view.DataContext!;
        var input = InputOf(view);

        var args = Press(input, Key.Enter, KeyModifiers.None);

        Assert.True(args.Handled);
        Assert.Equal("", model.InputText);
        Assert.Empty(model.Items);
        Assert.DoesNotContain('\n', input.Text ?? "");
    }

    private static TextBox InputOf(ChatView view) =>
        view.FindControl<TextBox>("Input") ?? throw new InvalidOperationException("The chat input is not in the view.");

    private static KeyEventArgs Press(TextBox target, Key key, KeyModifiers modifiers)
    {
        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            Source = target,
        };
        target.RaiseEvent(args);
        return args;
    }
}
