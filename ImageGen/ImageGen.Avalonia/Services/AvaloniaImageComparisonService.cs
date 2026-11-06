using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Services;

/// <summary>Shows two image snapshots in a modal comparison window and disposes their bitmaps on close.</summary>
public sealed class AvaloniaImageComparisonService(Func<Window?> owner, IDialogService dialogs) : IImageComparisonService
{
    private int _isOpen;

    public async Task ShowAsync(ComparisonPageModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (Interlocked.Exchange(ref _isOpen, 1) != 0) return;
        var parent = owner();
        if (parent is null)
        {
            Interlocked.Exchange(ref _isOpen, 0);
            return;
        }

        var window = new ComparisonWindow(model, parent, dialogs);
        try
        {
            if (cancellationToken.IsCancellationRequested) return;
            var showTask = window.ShowDialog(parent);
            using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(window.Close));
            window.StartLoading();
            await showTask;
        }
        finally
        {
            window.DisposeImages();
            Interlocked.Exchange(ref _isOpen, 0);
        }
    }
}

internal sealed class ComparisonWindow : Window
{
    internal ComparisonPanel Panel { get; }

    internal Bitmap? FirstBitmap => Panel.FirstBitmap;
    internal Bitmap? SecondBitmap => Panel.SecondBitmap;
    internal double FirstScale => Panel.FirstScale;
    internal bool IsOverlayLayout => Panel.IsOverlayLayout;

    public ComparisonWindow(ComparisonPageModel model, Window owner, IDialogService dialogs)
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        Width = Math.Max(owner.Bounds.Width, 720);
        Height = Math.Max(owner.Bounds.Height, 520);
        Panel = new ComparisonPanel(model, dialogs);
        Content = Panel;
        Panel.CloseRequested += (_, _) => Close();
        Closed += (_, _) => Panel.DisposeImages();
        Opened += (_, _) => Panel.Focus();
    }

    public void StartLoading() => Panel.StartLoading();

    public void DisposeImages() => Panel.DisposeImages();
}
