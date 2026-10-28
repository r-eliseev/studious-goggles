using Avalonia.Controls;
using Avalonia.Input.Platform;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>Copies text through the clipboard of the window that shows the chat.</summary>
public sealed class AvaloniaClipboardService(Func<Window?> owner) : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(owner())?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
