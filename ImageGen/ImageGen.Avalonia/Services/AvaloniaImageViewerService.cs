using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;

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
    private readonly IReadOnlyList<ImageViewerItem> _items;
    private readonly Image _image = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private readonly TextBlock _position = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White };
    private readonly IDialogService _dialogs;
    private Bitmap? _bitmap;
    private readonly ImageViewerNavigationState _navigation;
    private bool _started;

    internal int? DisplayedIndex => _navigation.DisplayedIndex;

    internal Bitmap? DisplayedBitmap => _bitmap;

    internal string? PositionText => _position.Text;

    public ImageViewerWindow(IReadOnlyList<ImageViewerItem> items, int initialIndex, Window owner, IDialogService dialogs)
    {
        _items = items;
        _navigation = new ImageViewerNavigationState(items.Count, initialIndex);
        _dialogs = dialogs;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowState = WindowState.Normal;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        Width = Math.Max(owner.Bounds.Width, 640);
        Height = Math.Max(owner.Bounds.Height, 480);
        Background = Brushes.Black;
        var close = MakeButton(AppResources.ImageViewerClose, Close);
        var previous = MakeButton(AppResources.ImageViewerPrevious, () => Navigate(-1));
        var next = MakeButton(AppResources.ImageViewerNext, () => Navigate(1));
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12) };
        toolbar.Children.Add(previous);
        Grid.SetColumn(_position, 1);
        toolbar.Children.Add(_position);
        Grid.SetColumn(next, 2);
        toolbar.Children.Add(next);
        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(12) };
        root.Children.Add(_image);
        Grid.SetRow(toolbar, 1);
        root.Children.Add(toolbar);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        root.Children.Add(close);
        Content = root;
        root.PointerPressed += (_, args) =>
        {
            if (ReferenceEquals(args.Source, root)) Close();
        };
        PointerPressed += (_, args) =>
        {
            if (ReferenceEquals(args.Source, this)) Close();
        };
        KeyDown += OnKeyDown;
        Closed += (_, _) => _navigation.Close();
        Opened += (_, _) => close.Focus();
    }

    public void StartLoading()
    {
        if (_started) return;
        _started = true;
        LoadAsync(_navigation.InitialRequest).FireAndForgetSafeAsync();
    }

    public void DisposeImage()
    {
        _navigation.Close();
        _image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private static Button MakeButton(string label, Action action)
    {
        var button = new Button { Content = label, MinWidth = 48, MinHeight = 48, Margin = new Thickness(4) };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.Left) Navigate(-1);
        else if (e.Key == Key.Right) Navigate(1);
    }

    private void Navigate(int delta)
    {
        if (_navigation.TryNavigate(delta, out var request)) LoadAsync(request).FireAndForgetSafeAsync();
    }

    private async Task LoadAsync((int Index, int Epoch) request)
    {
        try
        {
            var bitmap = await Task.Run(() =>
            {
                var bytes = File.ReadAllBytes(_items[request.Index].AbsolutePath);
                if (!IsRecognizedImage(bytes)) throw new InvalidDataException();
                using var stream = new MemoryStream(bytes, writable: false);
                var decoded = new Bitmap(stream);
                if (decoded.PixelSize.Width <= 1 || decoded.PixelSize.Height <= 1) { decoded.Dispose(); throw new InvalidDataException(); }
                return decoded;
            });
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_navigation.TryCommit(request)) { bitmap.Dispose(); return; }
                var old = _bitmap;
                _bitmap = bitmap;
                _image.Source = bitmap;
                AutomationProperties.SetName(_image, _items[request.Index].AccessibleLabel);
                _position.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.ImageViewerPositionFormat, request.Index + 1, _items.Count);
                old?.Dispose();
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            if (!_navigation.TryFail(request, out var failureEpoch, out var displayedIndex)) return;
            try
            {
                await _dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable);
            }
            finally
            {
                if (displayedIndex is null && _navigation.IsCurrentEpoch(failureEpoch)) Close();
            }
        }
    }

    private static bool IsRecognizedImage(byte[] bytes) => bytes.Length > 12 &&
        (bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
         || bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[^2] == 0xff && bytes[^1] == 0xd9
         || System.Text.Encoding.ASCII.GetString(bytes, 0, 4) is "GIF8" or "RIFF" or "BM");
}
