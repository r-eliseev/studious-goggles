using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Avalonia.Tests.Services;

public sealed class OverlayDialogServiceTests
{
    [AvaloniaFact]
    public async Task AlertAsync_OkPressed_RemovesOverlay()
    {
        var (owner, host, service) = CreateService();
        var shown = service.AlertAsync("Alert title", "Alert message");
        Dispatcher.UIThread.RunJobs();
        var overlay = GetOverlay(host);
        var card = GetCard(overlay);
        var message = Assert.Single(overlay.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Alert message");

        Assert.Equal("Alert title", AutomationProperties.GetName(card));
        Assert.Equal("Alert message", message.Text);
        Assert.NotNull(overlay.Background);
        Assert.Equal(480, card.MaxWidth);
        Assert.True(card.Bounds.Width <= host.Bounds.Width - card.Margin.Left - card.Margin.Right);
        ClickButton(overlay, "OK");

        await shown.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(host.Children);
        owner.Close();
    }

    [AvaloniaFact]
    public async Task ConfirmAsync_AcceptPressed_ReturnsTrue()
    {
        var (owner, host, service) = CreateService();
        var answer = service.ConfirmAsync("Confirm", "Continue?", "Yes", "No");
        ClickButton(GetOverlay(host), "Yes");

        Assert.True(await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task ConfirmAsync_CancelPressed_ReturnsFalse()
    {
        var (owner, host, service) = CreateService();
        var answer = service.ConfirmAsync("Confirm", "Continue?", "Yes", "No");
        ClickButton(GetOverlay(host), "No");

        Assert.False(await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task ConfirmAsync_DestructiveAccept_UsesDangerStyle()
    {
        var (owner, host, service) = CreateService();
        var answer = service.ConfirmAsync("Delete", "Delete this?", "Delete", "Cancel", destructive: true);
        var accept = FindButton(GetOverlay(host), "Delete");

        Assert.Contains("danger", accept.Classes);
        Assert.DoesNotContain("primary", accept.Classes);
        Click(accept);
        Assert.True(await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task PromptAsync_AcceptPressed_ReturnsEditedTextAndFocusesInput()
    {
        var (owner, host, service) = CreateService();
        var answer = service.PromptAsync("Prompt", "Name", "Save", "Cancel", "initial");
        Dispatcher.UIThread.RunJobs();
        var overlay = GetOverlay(host);
        var input = Assert.Single(overlay.GetVisualDescendants().OfType<TextBox>());

        Assert.Equal("initial", input.Text);
        Assert.True(input.IsFocused);
        input.Text = "updated";
        ClickButton(overlay, "Save");

        Assert.Equal("updated", await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task PromptAsync_CancelPressed_ReturnsNull()
    {
        var (owner, host, service) = CreateService();
        var answer = service.PromptAsync("Prompt", "Name", "Save", "Cancel", "initial");
        ClickButton(GetOverlay(host), "Cancel");

        Assert.Null(await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task ChooseAsync_OptionPressed_ReturnsOption()
    {
        var (owner, host, service) = CreateService();
        var answer = service.ChooseAsync("Choose", "Cancel", ["First", "Second", "Third"]);
        ClickButton(GetOverlay(host), "Second");

        Assert.Equal("Second", await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task ChooseAsync_CancelPressed_ReturnsNull()
    {
        var (owner, host, service) = CreateService();
        var answer = service.ChooseAsync("Choose", "Cancel", ["First", "Second"]);
        ClickButton(GetOverlay(host), "Cancel");

        Assert.Null(await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task TryCloseTop_OpenConfirmReturnsFalseAndWithoutDialogReturnsFalse()
    {
        var (owner, host, service) = CreateService();
        Assert.False(service.TryCloseTop());
        var answer = service.ConfirmAsync("Confirm", "Continue?", "Yes", "No");
        _ = GetOverlay(host);

        Assert.True(service.TryCloseTop());
        Assert.False(await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(host.Children);
        Assert.False(service.TryCloseTop());
        owner.Close();
    }

    [AvaloniaFact]
    public async Task ShowAsync_SecondDialogWaitsForFirstToClose()
    {
        var (owner, host, service) = CreateService();
        var first = service.AlertAsync("First", "First message");
        var firstOverlay = GetOverlay(host);
        var second = service.AlertAsync("Second", "Second message");

        Assert.Single(host.Children);
        Assert.Contains(firstOverlay.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "First");
        ClickButton(firstOverlay, "OK");
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => host.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Second"));
        ClickButton(GetOverlay(host), "OK");
        await second.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(host.Children);
        owner.Close();
    }

    [AvaloniaFact]
    public async Task ConfirmAsync_MissingHost_ReturnsFalseAndLogsWarning()
    {
        var logger = new CapturingLogger<OverlayDialogService>();
        var service = new OverlayDialogService(() => null, logger);

        Assert.False(await service.ConfirmAsync("Confirm", "Continue?", "Yes", "No"));

        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
    }

    [AvaloniaFact]
    public async Task Enter_PressedOnOverlay_AcceptsDialog()
    {
        var (owner, host, service) = CreateService();
        var answer = service.ConfirmAsync("Confirm", "Continue?", "Yes", "No");
        var overlay = GetOverlay(host);
        overlay.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        Assert.True(await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        owner.Close();
    }

    [AvaloniaFact]
    public async Task Escape_PressedOnOverlay_CancelsDialog()
    {
        var (owner, host, service) = CreateService();
        var answer = service.ConfirmAsync("Confirm", "Continue?", "Yes", "No");
        var overlay = GetOverlay(host);
        overlay.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

        Assert.False(await answer.WaitAsync(TimeSpan.FromSeconds(5)));
        owner.Close();
    }

    private static (Window Owner, Grid Host, OverlayDialogService Service) CreateService()
    {
        var host = new Grid();
        var owner = new Window { Content = host, Width = 320, Height = 480 };
        owner.Show();
        return (owner, host, new OverlayDialogService(() => host, NullLogger<OverlayDialogService>.Instance));
    }

    private static Grid GetOverlay(Grid host)
    {
        Dispatcher.UIThread.RunJobs();
        return Assert.IsType<Grid>(Assert.Single(host.Children));
    }

    private static Border GetCard(Grid overlay) => Assert.IsType<Border>(Assert.Single(overlay.Children));

    private static void ClickButton(Control root, string name) => Click(FindButton(root, name));

    private static Button FindButton(Control root, string name) => Assert.Single(root.GetVisualDescendants().OfType<Button>(), button => AutomationProperties.GetName(button) == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, timeout.Token);
        }
    }
}
