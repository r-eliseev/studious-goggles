using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>Displays image snapshots in an owned modal window sized to the owner client area.</summary>
public sealed class AvaloniaImageViewerService(
    Func<Window?> owner,
    AvaloniaFullScreenController fullScreen,
    IDialogService dialogs) : IImageViewerService
{
    private int _isOpen;

    public async Task ShowAsync(IReadOnlyList<ImageViewerItem> items, int initialIndex, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0 || (uint)initialIndex >= (uint)items.Count) throw new ArgumentOutOfRangeException(nameof(initialIndex));
        if (Interlocked.Exchange(ref _isOpen, 1) != 0) return;
        var parent = owner();
        if (parent is null)
        {
            Interlocked.Exchange(ref _isOpen, 0);
            return;
        }

        var snapshot = items.ToArray();
        var window = new ImageViewerWindow(snapshot, initialIndex, parent, dialogs);
        IDisposable? fullScreenLease = null;
        try
        {
            if (cancellationToken.IsCancellationRequested) return;
            fullScreenLease = fullScreen.Enter(window);
            var showTask = window.ShowDialog(parent);
            using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(window.Close));
            window.StartLoading();
            await showTask;
        }
        finally
        {
            fullScreenLease?.Dispose();
            window.DisposeImage();
            Interlocked.Exchange(ref _isOpen, 0);
        }
    }
}

internal sealed class ImageViewerWindow : Window
{
    internal ImageViewerPanel Panel { get; }

    internal int? DisplayedIndex => Panel.DisplayedIndex;

    internal Bitmap? DisplayedBitmap => Panel.DisplayedBitmap;

    internal string? PositionText => Panel.PositionText;

    public ImageViewerWindow(IReadOnlyList<ImageViewerItem> items, int initialIndex, Window owner, IDialogService dialogs)
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowState = WindowState.Normal;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        Width = Math.Max(owner.Bounds.Width, 640);
        Height = Math.Max(owner.Bounds.Height, 480);
        Panel = new ImageViewerPanel(items, initialIndex, dialogs);
        Content = Panel;
        Panel.CloseRequested += (_, _) => Close();
        Closed += (_, _) => Panel.DisposeImage();
        Opened += (_, _) => Panel.Focus();
    }

    public void StartLoading() => Panel.StartLoading();

    public void DisposeImage() => Panel.DisposeImage();
}
