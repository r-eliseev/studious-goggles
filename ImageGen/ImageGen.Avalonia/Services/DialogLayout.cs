using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ImageGen.Avalonia.Views;

namespace ImageGen.Avalonia.Services;

/// <summary>The parts of a dialog window: the content and the controls the window needs to answer.</summary>
public sealed record DialogParts(Control Content, Panel Buttons, Button Accept, Button? Cancel, TextBox? Input);

/// <summary>
/// Builds the content of an alert, confirm or prompt window (docs/design/implementation.md, section 7 and task 072).
/// The button order and the destructive style depend on the arguments, so the layout is testable without a window.
/// </summary>
public static class DialogLayout
{
    public static DialogParts Build(
        string title,
        string message,
        string accept,
        string? cancel,
        string? input,
        bool multiline,
        bool destructive,
        bool isMacOs)
    {
        var titleText = new TextBlock
        {
            Text = title,
            Classes = { "title" },
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        var body = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };

        var text = input is null
            ? null
            : new TextBox { Text = input, MaxLength = 4000, AcceptsReturn = multiline, TextWrapping = TextWrapping.Wrap };
        if (text is not null)
        {
            text.MinHeight = multiline ? 96 : 0;
            AutomationProperties.SetName(text, message);
        }

        var acceptButton = new Button { Content = accept, Classes = { destructive ? "danger" : "primary" } };
        AutomationProperties.SetName(acceptButton, accept);

        Button? cancelButton = null;
        if (cancel is not null)
        {
            cancelButton = new Button { Content = cancel };
            AutomationProperties.SetName(cancelButton, cancel);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        if (cancelButton is null)
        {
            buttons.Children.Add(acceptButton);
        }
        else
        {
            DialogButtonOrder.Apply(buttons, acceptButton, cancelButton, isMacOs);
        }

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(titleText);
        stack.Children.Add(body);
        if (text is not null)
        {
            stack.Children.Add(text);
        }

        stack.Children.Add(buttons);

        var content = new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(12),
            Background = Application.Current?.TryFindResource("IgSurfaceBrush", out var brush) == true ? brush as IBrush : null,
            Child = stack,
        };
        return new DialogParts(content, buttons, acceptButton, cancelButton, text);
    }
}
