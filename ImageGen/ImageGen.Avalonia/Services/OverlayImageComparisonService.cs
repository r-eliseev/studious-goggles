using Avalonia.Controls;
using Avalonia.Threading;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>Displays an image comparison over the currently hosted page.</summary>
public sealed class OverlayImageComparisonService(Func<Panel?> host, IDialogService dialogs) : IImageComparisonService
{
    private int _isOpen;
    private ComparisonPanel? _openPanel;

    public async Task ShowAsync(ComparisonPageModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (Interlocked.Exchange(ref _isOpen, 1) != 0)
        {
            return;
        }

        Panel? panelHost = null;
        ComparisonPanel? panel = null;
        EventHandler? closed = null;
        try
        {
            panelHost = host();
            if (panelHost is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            panel = new ComparisonPanel(model, dialogs);
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

                panel.DisposeImages();
                Interlocked.CompareExchange(ref _openPanel, null, panel);
            }

            Interlocked.Exchange(ref _isOpen, 0);
        }
    }

    /// <summary>Closes the active comparison panel for the system Back action.</summary>
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
