using Avalonia.Controls;

namespace ImageGen.Avalonia.Views;

/// <summary>
/// Order of the buttons of a dialog-like screen (docs/design/implementation.md, section 7): Save first on Windows and
/// Linux, Cancel first on macOS.
/// </summary>
public static class DialogButtonOrder
{
    public static void Apply(Panel row, Control save, Control cancel) => Apply(row, save, cancel, OperatingSystem.IsMacOS());

    /// <param name="isMacOs">Passed in so that the order can be tested on any platform.</param>
    public static void Apply(Panel row, Control save, Control cancel, bool isMacOs)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(cancel);

        row.Children.Clear();
        if (isMacOs)
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
