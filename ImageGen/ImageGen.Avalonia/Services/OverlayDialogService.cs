using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Services;

/// <summary>Shows dialogs as a modal overlay inside the panel hosting the current page.</summary>
public sealed class OverlayDialogService(Func<Panel?> host, ILogger<OverlayDialogService> logger) : IDialogService
{
    private readonly SemaphoreSlim _dialogQueue = new(1, 1);
    private readonly object _activeDialogLock = new();
    private Action? _closeTop;

    public async Task AlertAsync(string title, string message)
    {
        var parts = DialogLayout.Build(title, message, AppResources.Ok, cancel: null, input: null, multiline: false, destructive: false, OperatingSystem.IsMacOS());
        var card = PrepareCard(parts.Content, title);
        _ = await ShowAsync(card, parts.Accept, () => true, cancelValue: false, (accept, _) =>
            parts.Accept.Click += (_, _) => accept(true));
    }

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        ConfirmAsync(title, message, accept, cancel, destructive: false);

    public async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel, bool destructive)
    {
        var parts = DialogLayout.Build(title, message, accept, cancel, input: null, multiline: false, destructive, OperatingSystem.IsMacOS());
        var card = PrepareCard(parts.Content, title);
        return await ShowAsync(card, parts.Accept, () => true, cancelValue: false, (complete, close) =>
        {
            parts.Accept.Click += (_, _) => complete(true);
            parts.Cancel!.Click += (_, _) => close();
        });
    }

    public async Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue)
    {
        var parts = DialogLayout.Build(title, message, accept, cancel, initialValue, multiline: true, destructive: false, OperatingSystem.IsMacOS());
        var card = PrepareCard(parts.Content, title);
        var input = parts.Input!;
        return await ShowAsync(card, input, () => input.Text, cancelValue: null, (complete, close) =>
        {
            parts.Accept.Click += (_, _) => complete(input.Text);
            parts.Cancel!.Click += (_, _) => close();
        });
    }

    public async Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var content = new StackPanel { Spacing = 12 };
        if (title.Length > 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = title,
                Classes = { "title" },
                FontSize = 17,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var optionButtons = new List<Button>(options.Count);
        foreach (var option in options)
        {
            var button = new Button { Content = option, HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(button, option);
            optionButtons.Add(button);
            content.Children.Add(button);
        }

        var cancelButton = new Button { Content = cancel, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(cancelButton, cancel);
        content.Children.Add(cancelButton);

        var card = PrepareCard(new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(12),
            Background = Application.Current?.TryFindResource("IgSurfaceBrush", out var brush) == true ? brush as IBrush : null,
            Child = content,
        }, title);
        var firstOption = options.FirstOrDefault();
        var initialFocus = (Control?)optionButtons.FirstOrDefault() ?? cancelButton;

        return await ShowAsync(card, initialFocus, () => firstOption, cancelValue: null, (complete, close) =>
        {
            for (var index = 0; index < optionButtons.Count; index++)
            {
                var selected = options[index];
                optionButtons[index].Click += (_, _) => complete(selected);
            }

            cancelButton.Click += (_, _) => close();
        });
    }

    /// <summary>Closes the active dialog as if its cancel button had been pressed.</summary>
    public bool TryCloseTop()
    {
        Action? close;
        lock (_activeDialogLock)
        {
            close = _closeTop;
        }

        if (close is null)
        {
            return false;
        }

        close();
        return true;
    }

    private async Task<T> ShowAsync<T>(
        Border card,
        Control initialFocus,
        Func<T> acceptValue,
        T cancelValue,
        Action<Action<T>, Action> connectButtons)
    {
        await _dialogQueue.WaitAsync();
        Grid? overlay = null;
        Panel? panel = null;
        Action? close = null;
        try
        {
            panel = host();
            if (panel is null)
            {
                logger.LogWarning("Cannot show overlay dialog because the host panel is unavailable.");
                return cancelValue;
            }

            var answer = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<T> accept = value => answer.TrySetResult(value);
            close = () => answer.TrySetResult(cancelValue);
            connectButtons(accept, close);

            overlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                Focusable = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            overlay.Children.Add(card);
            overlay.AddHandler(InputElement.KeyDownEvent, (_, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    close();
                    args.Handled = true;
                }
                else if (args.Key == Key.Enter)
                {
                    accept(acceptValue());
                    args.Handled = true;
                }
            }, RoutingStrategies.Bubble);

            panel.Children.Add(overlay);
            lock (_activeDialogLock)
            {
                _closeTop = close;
            }

            Dispatcher.UIThread.Post(() => initialFocus.Focus(), DispatcherPriority.Input);
            return await answer.Task;
        }
        finally
        {
            if (overlay is not null && panel?.Children.Contains(overlay) == true)
            {
                panel.Children.Remove(overlay);
            }

            lock (_activeDialogLock)
            {
                if (close is not null && ReferenceEquals(_closeTop, close))
                {
                    _closeTop = null;
                }
            }

            _dialogQueue.Release();
        }
    }

    private static Border PrepareCard(Control content, string title)
    {
        var card = (Border)content;
        card.MaxWidth = 480;
        card.Margin = new Thickness(16);
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(card, title);
        return card;
    }
}
