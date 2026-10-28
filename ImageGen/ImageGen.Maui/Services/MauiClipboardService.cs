using ImageGen.Presentation.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace ImageGen.Maui.Services;

public sealed class MauiClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Clipboard.Default.SetTextAsync(text);
}
