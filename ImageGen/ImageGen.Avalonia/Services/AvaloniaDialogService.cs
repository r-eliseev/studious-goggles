using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>
/// Alert, confirm and prompt as small windows over the main window. The content comes from <see cref="DialogLayout"/>.
/// The windows are not modal: the call waits for the answer, the rest of the window stays usable (the owner is set, so
/// the window stays above it). Escape closes the window as a cancel; Enter answers a single-line prompt.
/// </summary>
public sealed class AvaloniaDialogService(Func<Window?> owner) : IDialogService
{
    public async Task AlertAsync(string title, string message) =>
        await ShowAsync(title, message, AppResources.Ok, cancel: null, input: null, multiline: false, destructive: false);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        ConfirmAsync(title, message, accept, cancel, destructive: false);

    public async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel, bool destructive)
    {
        var outcome = await ShowAsync(title, message, accept, cancel, input: null, multiline: false, destructive);
        return outcome.Accepted;
    }

    public async Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue)
    {
        var outcome = await ShowAsync(title, message, accept, cancel, input: initialValue, multiline: true, destructive: false);
        return outcome.Accepted ? outcome.Text : null;
    }

    private async Task<(bool Accepted, string? Text)> ShowAsync(
        string title,
        string message,
        string accept,
        string? cancel,
        string? input,
        bool multiline,
        bool destructive)
    {
        var answer = new TaskCompletionSource<(bool Accepted, string? Text)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var window = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
        };

        var parts = DialogLayout.Build(title, message, accept, cancel, input, multiline, destructive, OperatingSystem.IsMacOS());
        parts.Accept.Click += (_, _) =>
        {
            answer.TrySetResult((true, parts.Input?.Text));
            window.Close();
        };
        if (parts.Cancel is not null)
        {
            parts.Cancel.Click += (_, _) => window.Close();
        }

        // Escape closes the window; it counts as a cancel, so the answer stays "not accepted".
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                window.Close();
                e.Handled = true;
            }
        };
        if (parts.Input is { AcceptsReturn: false } single)
        {
            single.AddHandler(InputElement.KeyDownEvent, (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    answer.TrySetResult((true, single.Text));
                    window.Close();
                    e.Handled = true;
                }
            }, RoutingStrategies.Tunnel);
        }

        window.Content = parts.Content;
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
