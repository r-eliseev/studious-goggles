using ImageGen.Core.Storage;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Services;

/// <summary>Shows an image snapshot in a modal page without changing platform immersive state.</summary>
public sealed class MauiImageViewerService(IMauiFullScreenController fullScreen, IImageFileStore files) : IImageViewerService
{
    private int _isOpen;

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

        var page = new MauiImageViewerPage(items.ToArray(), initialIndex, files, cancellationToken);
        using var registration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(page.Close));
        MauiViewerFullscreenSession? fullscreenSession = null;
        try
        {
            await Shell.Current.Navigation.PushModalAsync(page);
            if (page.Window is { } window)
            {
                fullscreenSession = new MauiViewerFullscreenSession(window, fullScreen);
            }
            page.StartLoading();
            await page.Closed;
        }
        finally
        {
            fullscreenSession?.Dispose();
            if (Shell.Current.Navigation.ModalStack.LastOrDefault() == page)
            {
                await Shell.Current.Navigation.PopModalAsync();
            }
            Interlocked.Exchange(ref _isOpen, 0);
        }
    }
}

internal sealed class MauiImageViewerPage : ContentPage
{
    private readonly IReadOnlyList<ImageViewerItem> _items;
    private readonly IImageFileStore _files;
    private readonly CancellationToken _cancellationToken;
    private readonly Image _image = new() { Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
    private readonly Label _position = new() { TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
    private readonly Button _closeButton;
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ImageViewerNavigationState _navigation;
    private ImageDimensions? _imageDimensions;
    private bool _closing;
    private bool _started;
#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement? _nativeView;
#endif

    public MauiImageViewerPage(IReadOnlyList<ImageViewerItem> items, int initialIndex, IImageFileStore files, CancellationToken cancellationToken)
    {
        _items = items;
        _files = files;
        _cancellationToken = cancellationToken;
        _navigation = new ImageViewerNavigationState(items.Count, initialIndex);
        BackgroundColor = Colors.Black;
        _closeButton = MakeButton(AppResources.ImageViewerClose, Close);
        var previous = MakeButton(AppResources.ImageViewerPrevious, () => Navigate(-1));
        var next = MakeButton(AppResources.ImageViewerNext, () => Navigate(1));
        var controls = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Padding = 12 };
        controls.Add(previous, 0, 0);
        controls.Add(_position, 1, 0);
        controls.Add(next, 2, 0);
        var root = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }, Padding = 12 };
        root.Add(_image, 0, 0);
        root.Add(controls, 0, 1);
        root.Add(_closeButton, 0, 0);
        _closeButton.HorizontalOptions = LayoutOptions.End;
        _closeButton.VerticalOptions = LayoutOptions.Start;
        var backgroundTap = new TapGestureRecognizer();
        backgroundTap.Tapped += (_, args) =>
        {
            var point = args.GetPosition(root);
            ImageViewerPoint? tap = point is { } value ? new ImageViewerPoint(value.X, value.Y) : null;
            var imageBounds = ToViewerRect(_image.Bounds);
            var visibleImage = _imageDimensions is { } dimensions
                ? ImageViewerHitTest.GetAspectFitRect(imageBounds, dimensions.Width, dimensions.Height)
                : null;
            var protectedControls = new[] { ToViewerRect(controls.Bounds), ToViewerRect(_closeButton.Bounds) };
            if (ImageViewerHitTest.IsBackgroundTap(tap, imageBounds, visibleImage, protectedControls, _imageDimensions is not null)) Close();
        };
        root.GestureRecognizers.Add(backgroundTap);
        Content = root;
    }

    public Task Closed => _closed.Task;

    public void StartLoading()
    {
        if (_started || _closing) return;
        _started = true;
        LoadAsync(_navigation.InitialRequest).FireAndForgetSafeAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        Close();
        return true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MainThread.BeginInvokeOnMainThread(() => _closeButton.Focus());
    }

#if WINDOWS
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (_nativeView is not null)
        {
            _nativeView.KeyDown -= OnWindowsKeyDown;
        }
        _nativeView = Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
        if (_nativeView is null) return;
        _nativeView.KeyDown += OnWindowsKeyDown;
        _nativeView.IsTabStop = true;
    }

    private void OnWindowsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            Close();
            args.Handled = true;
        }
    }
#endif

    public void Close()
    {
        if (_closing) return;
        _closing = true;
        _navigation.Close();
        _closed.TrySetResult();
    }

    private static Button MakeButton(string label, Action action)
    {
        var button = new Button { Text = label, MinimumWidthRequest = 48, MinimumHeightRequest = 48, Padding = 8 };
        SemanticProperties.SetDescription(button, label);
        button.Clicked += (_, _) => action();
        return button;
    }

    private static ImageViewerRect ToViewerRect(Rect bounds) =>
        new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private void Navigate(int delta)
    {
        if (_navigation.TryNavigate(delta, out var request)) LoadAsync(request).FireAndForgetSafeAsync();
    }

    private async Task LoadAsync((int Index, int Epoch) request)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(_items[request.Index].AbsolutePath, _cancellationToken);
            if (!ImageBytesAreReadable(bytes)) throw new InvalidDataException();
            var relativePath = _items[request.Index].RelativePath;
            var dimensions = string.IsNullOrWhiteSpace(relativePath)
                ? null
                : await _files.TryReadDimensionsAsync(relativePath, _cancellationToken);
            var source = ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_closing || !_navigation.TryCommit(request)) return;
                _imageDimensions = dimensions;
                _image.Source = source;
                SemanticProperties.SetDescription(_image, _items[request.Index].AccessibleLabel);
                _position.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.ImageViewerPositionFormat, request.Index + 1, _items.Count);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            if (_closing || !_navigation.TryFail(request, out var failureEpoch, out var displayedIndex)) return;
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () => await DisplayAlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable, AppResources.Ok));
            }
            finally
            {
                if (displayedIndex is null && !_closing && _navigation.IsCurrentEpoch(failureEpoch)) Close();
            }
        }
    }

    private static bool ImageBytesAreReadable(byte[] bytes) =>
        bytes.Length > 12 && ((bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            || (bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[^2] == 0xff && bytes[^1] == 0xd9)
            || System.Text.Encoding.ASCII.GetString(bytes, 0, 4) is "GIF8" or "RIFF" or "BM");
}
