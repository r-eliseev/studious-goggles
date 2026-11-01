using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace ImageGen.Maui.Platforms.Windows;

/// <summary>
/// Opens the card's context menu for its "…" button, the same menu a right click opens. MAUI puts the card's
/// ContextFlyout on its WinUI element; the button has none, so the search walks up the visual tree to the card.
/// </summary>
public static class CardActionsFlyout
{
    /// <summary>Returns false when no card with a context menu is above the button.</summary>
    public static bool ShowFor(FrameworkElement button)
    {
        DependencyObject? node = button;
        while (node is not null)
        {
            if (node is UIElement { ContextFlyout: { } flyout })
            {
                flyout.ShowAt(button);
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }
}
