using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>
/// Alert, confirm and prompt as small windows over the main window. The windows are built in code: they are short and
/// every control gets its <c>AutomationProperties.Name</c>. They are not modal: the call waits for the answer, the rest of
/// the window stays usable (the owner is set, so the window stays above it).
/// </summary>
public sealed class AvaloniaDialogService(Func<Window?> owner) : IDialogService
{
    public async Task AlertAsync(string title, string message) =>
        await ShowAsync(title, message, AppResources.Ok, cancel: null, input: null);

    public async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        var outcome = await ShowAsync(title, message, accept, cancel, input: null);
        return outcome.Accepted;
    }

    public async Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue)
    {
        var outcome = await ShowAsync(title, message, accept, cancel, input: initialValue);
        return outcome.Accepted ? outcome.Text : null;
    }

    private async Task<(bool Accepted, string? Text)> ShowAsync(
        string title,
        string message,
        string accept,
        string? cancel,
        string? input)
    {
        var answer = new TaskCompletionSource<(bool Accepted, string? Text)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var window = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
        };

        var text = input is null ? null : new TextBox { Text = input, MaxLength = 4000 };
        if (text is not null)
        {
            AutomationProperties.SetName(text, message);
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var acceptButton = new Button { Content = accept, MinHeight = 44, MinWidth = 88 };
        AutomationProperties.SetName(acceptButton, accept);
        acceptButton.Click += (_, _) =>
        {
            answer.TrySetResult((true, text?.Text));
            window.Close();
        };
        buttons.Children.Add(acceptButton);

        if (cancel is not null)
        {
            var cancelButton = new Button { Content = cancel, MinHeight = 44, MinWidth = 88 };
            AutomationProperties.SetName(cancelButton, cancel);
            cancelButton.Click += (_, _) => window.Close();
            buttons.Children.Add(cancelButton);
        }

        var content = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        if (text is not null)
        {
            content.Children.Add(text);
        }

        content.Children.Add(buttons);
        window.Content = content;
        window.Closed += (_, _) => answer.TrySetResult((false, null));

        var parent = owner();
        if (parent is null)
        {
            window.Show();
        }
        else
        {
            window.Show(parent);
        }

        return await answer.Task;
    }
}
