using Avalonia.Controls;
using Avalonia.Threading;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>Displays an image viewer panel over the currently hosted page.</summary>
public sealed class OverlayImageViewerService(Func<Panel?> host, IDialogService dialogs) : IImageViewerService
{
    private int _isOpen;
    private ImageViewerPanel? _openPanel;

    public async Task ShowAsync(IReadOnlyList<ImageViewerItem> items, int initialIndex, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0 || (uint)initialIndex >= (uint)items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(initialIndex));
        }

        if (Interlocked.Exchange(ref _isOpen, 1) != 0)
        {
            return;
        }

        Panel? panelHost = null;
        ImageViewerPanel? panel = null;
        EventHandler? closed = null;
        try
        {
            panelHost = host();
            if (panelHost is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            panel = new ImageViewerPanel(items.ToArray(), initialIndex, dialogs);
            closed = (_, _) => completion.TrySetResult();
            panel.CloseRequested += closed;
            panelHost.Children.Add(panel);
            Volatile.Write(ref _openPanel, panel);
            using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(panel.RequestClose));
            panel.Focus();
            panel.StartLoading();
            await completion.Task;
        }
        finally
        {
            if (panel is not null)
            {
                if (closed is not null)
                {
                    panel.CloseRequested -= closed;
                }

                if (panelHost?.Children.Contains(panel) == true)
                {
                    panelHost.Children.Remove(panel);
                }

                panel.DisposeImage();
                Interlocked.CompareExchange(ref _openPanel, null, panel);
            }

            Interlocked.Exchange(ref _isOpen, 0);
        }
    }

    /// <summary>Closes the active viewer panel for the system Back action.</summary>
    public bool TryCloseTop()
    {
        var panel = Volatile.Read(ref _openPanel);
        if (panel is null)
        {
            return false;
        }

        Dispatcher.UIThread.Post(panel.RequestClose);
        return true;
    }
}
