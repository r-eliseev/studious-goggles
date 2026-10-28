using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Presentation.PageModels;

/// <summary>Copies one fixed text to the clipboard and shows «Copied» for a short time.</summary>
public sealed partial class CopyTextAction : ObservableObject
{
    private static readonly TimeSpan DefaultCopiedFor = TimeSpan.FromSeconds(2);

    private readonly IClipboardService _clipboard;
    private readonly IMainThread _mainThread;
    private readonly string _text;
    private readonly TimeSpan _copiedFor;

    public CopyTextAction(IClipboardService clipboard, IMainThread mainThread, string text, TimeSpan? copiedFor = null)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _mainThread = mainThread ?? throw new ArgumentNullException(nameof(mainThread));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _copiedFor = copiedFor ?? DefaultCopiedFor;
    }

    /// <summary>True for a short time after a successful copy.</summary>
    [ObservableProperty]
    private bool _isCopied;

    [RelayCommand]
    private async Task CopyAsync()
    {
        await _clipboard.SetTextAsync(_text);
        IsCopied = true;
        ResetCopiedAfterDelayAsync().FireAndForgetSafeAsync();
    }

    private async Task ResetCopiedAfterDelayAsync()
    {
        await Task.Delay(_copiedFor);
        _mainThread.BeginInvokeOnMainThread(() => IsCopied = false);
    }
}
