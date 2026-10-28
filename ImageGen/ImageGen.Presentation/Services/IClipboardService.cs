namespace ImageGen.Presentation.Services;

/// <summary>Puts text on the system clipboard. Each shell implements it (MAUI: <c>Clipboard</c>, Avalonia: <c>TopLevel.Clipboard</c>).</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
}
