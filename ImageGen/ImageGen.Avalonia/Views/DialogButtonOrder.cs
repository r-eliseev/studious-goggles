using Avalonia.Controls;

namespace ImageGen.Avalonia.Views;

/// <summary>
/// Order of the buttons of a dialog-like screen (docs/design/implementation.md, section 7): Save first on Windows and
/// Linux, Cancel first on macOS.
/// </summary>
public static class DialogButtonOrder
{
    public static void Apply(Panel row, Control save, Control cancel)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(cancel);

        row.Children.Clear();
        if (OperatingSystem.IsMacOS())
        {
            row.Children.Add(cancel);
            row.Children.Add(save);
        }
        else
        {
            row.Children.Add(save);
            row.Children.Add(cancel);
        }
    }
}
