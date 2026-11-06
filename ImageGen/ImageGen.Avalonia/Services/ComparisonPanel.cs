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

/// <summary>The comparison content hosted by a desktop window or an in-page overlay.</summary>
internal sealed class ComparisonPanel : UserControl
{
    private readonly ComparisonPageModel _model;
    private bool _closed;

    internal event EventHandler? CloseRequested;
    private readonly Image _first = MakeImage();
    private readonly Image _second = MakeImage();
    private readonly Image _overlayFirstImage = MakeImage();
    private readonly Image _overlaySecondImage = MakeImage();
    private readonly Border _overlayClip = new() { ClipToBounds = true };
    private readonly Slider _slider = new() { Minimum = 0, Maximum = 1, Value = 0.5, MinWidth = 180 };
    private readonly TextBlock _firstMetadata = new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _secondMetadata = new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _overlayFirstMetadata = new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _overlaySecondMetadata = new() { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
    private readonly Grid _viewports = new() { ColumnDefinitions = new ColumnDefinitions("*,*") };
    private readonly Grid _overlay = new() { RowDefinitions = new RowDefinitions("*,Auto") };
    private Border _firstViewport = null!;
    private Border _secondViewport = null!;
    private Grid _firstPanel = null!;
    private Grid _secondPanel = null!;
    private readonly IDialogService _dialogs;
    private Bitmap? _firstBitmap;
    private Bitmap? _secondBitmap;
    private Point? _panStart;
    private double _panStartX;
    private double _panStartY;
    internal Bitmap? FirstBitmap => _firstBitmap;
    internal Bitmap? SecondBitmap => _secondBitmap;
    internal double FirstScale => (_first.RenderTransform as TransformGroup)?.Children.OfType<ScaleTransform>().FirstOrDefault()?.ScaleX ?? 1;
    internal bool IsOverlayLayout => _viewports.ColumnDefinitions.Count == 1;

    public ComparisonPanel(ComparisonPageModel model, IDialogService dialogs)
    {
        _model = model;
        _dialogs = dialogs;
        Focusable = true;
        Background = Brushes.Black;
        _firstViewport = MakeViewport(_first);
        _secondViewport = MakeViewport(_second);
        _firstPanel = MakePanel(_firstViewport, _firstMetadata);
        _secondPanel = MakePanel(_secondViewport, _secondMetadata);
        _overlay.Children.Add(MakeViewport(_overlayFirstImage));
        _overlayClip.Child = MakeViewport(_overlaySecondImage);
        _overlay.Children.Add(_overlayClip);
        _firstMetadata.Text = Metadata(model.First);
        _secondMetadata.Text = Metadata(model.Second);
        _overlayFirstMetadata.Text = Metadata(model.First);
        _overlaySecondMetadata.Text = Metadata(model.Second);
        var overlayMetadata = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 16 };
        overlayMetadata.Children.Add(_overlayFirstMetadata);
        overlayMetadata.Children.Add(_overlaySecondMetadata);
        Grid.SetRow(overlayMetadata, 1);
        _overlay.Children.Add(overlayMetadata);
        RefreshMode();
        _viewports.SizeChanged += (_, _) => UpdateOverlayClip();

        var sideBySide = MakeButton(AppResources.ComparisonSideBySide, () => _model.Mode = ComparisonMode.SideBySide);
        var overlay = MakeButton(AppResources.ComparisonOverlay, () => _model.Mode = ComparisonMode.Overlay);
        var close = MakeButton(AppResources.ComparisonClose, RequestClose);
        var zoom = new Slider { Minimum = 1, Maximum = 8, Value = 1, MinWidth = 160 };
        zoom.PropertyChanged += (_, args) =>
        {
            if (args.Property == RangeBase.ValueProperty)
            {
                _model.SetZoom(zoom.Value);
                ApplyTransforms();
            }
        };
        _slider.PropertyChanged += (_, args) =>
        {
            if (args.Property == RangeBase.ValueProperty)
            {
                _model.SliderFraction = _slider.Value;
                UpdateOverlayClip();
            }
        };
        _viewports.SizeChanged += (_, _) => UpdateOverlayClip();
        foreach (var viewport in new Control[] { _firstViewport, _secondViewport, _overlay })
        {
            viewport.PointerWheelChanged += OnWheel;
            viewport.PointerPressed += OnPanPressed;
            viewport.PointerMoved += OnPanMoved;
            viewport.PointerReleased += OnPanReleased;
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Children.Add(sideBySide);
        buttons.Children.Add(overlay);
        buttons.Children.Add(new TextBlock { Text = AppResources.ComparisonZoom, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
        buttons.Children.Add(zoom);
        buttons.Children.Add(_slider);
        buttons.Children.Add(close);
        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(12) };
        root.Children.Add(_viewports);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);
        Content = root;
        KeyDown += OnKeyDown;
        _model.PropertyChanged += OnModelChanged;
        close.Focus();
    }

    internal void StartLoading() => LoadBothAsync().FireAndForgetSafeAsync();

    internal void DisposeImages()
    {
        _closed = true;
        _model.PropertyChanged -= OnModelChanged;
        _first.Source = null;
        _second.Source = null;
        _overlayFirstImage.Source = null;
        _overlaySecondImage.Source = null;
        _firstBitmap?.Dispose();
        _secondBitmap?.Dispose();
        _firstBitmap = null;
        _secondBitmap = null;
    }

    private async Task LoadBothAsync()
    {
        try
        {
            var images = await Task.Run(() =>
            {
                Bitmap? first = null;
                try
                {
                    first = Load(_model.First.AbsolutePath);
                    var second = Load(_model.Second.AbsolutePath);
                    return (first, second);
                }
                catch
                {
                    first?.Dispose();
                    throw;
                }
            });
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_closed)
                {
                    images.Item1.Dispose();
                    images.Item2.Dispose();
                    return;
                }

                _firstBitmap = images.Item1;
                _secondBitmap = images.Item2;
                _first.Source = _firstBitmap;
                _second.Source = _secondBitmap;
                _overlayFirstImage.Source = _firstBitmap;
                _overlaySecondImage.Source = _secondBitmap;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            await _dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.ImageViewerUnreadable);
            await Dispatcher.UIThread.InvokeAsync(RequestClose);
        }
    }

    private static Bitmap Load(string path)
    {
        using var stream = File.OpenRead(path);
        return new Bitmap(stream);
    }

    private static Image MakeImage() => new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

    private static Border MakeViewport(Image image)
    {
        return new Border { Background = Brushes.Black, ClipToBounds = true, Child = image, Margin = new Thickness(6) };
    }

    private static Grid MakePanel(Border viewport, TextBlock metadata)
    {
        var panel = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(6) };
        panel.Children.Add(viewport);
        Grid.SetRow(metadata, 1);
        panel.Children.Add(metadata);
        return panel;
    }

    private static Button MakeButton(string label, Action action)
    {
        var button = new Button { Content = label, MinWidth = 48, MinHeight = 48, Padding = new Thickness(8) };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private static string Metadata(ComparisonImage image) => string.Join(" · ", image.ModelText, image.SeedText, image.CostText);

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ComparisonPageModel.Mode)) RefreshMode();
        if (args.PropertyName == nameof(ComparisonPageModel.SliderFraction)) UpdateOverlayClip();
        if (args.PropertyName is nameof(ComparisonPageModel.Zoom) or nameof(ComparisonPageModel.OffsetX) or nameof(ComparisonPageModel.OffsetY)) ApplyTransforms();
    }

    internal void RequestClose()
    {
        if (_closed) return;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape) RequestClose();
        else if (args.Key == Key.Left) _model.SliderFraction = Math.Clamp(_model.SliderFraction - 0.02, 0, 1);
        else if (args.Key == Key.Right) _model.SliderFraction = Math.Clamp(_model.SliderFraction + 0.02, 0, 1);
    }

    private void UpdateOverlayClip()
    {
        _overlayClip.Clip = _model.Mode == ComparisonMode.Overlay
            ? new RectangleGeometry(new Rect(0, 0,
                Math.Max(0, _overlay.Bounds.Width * _model.SliderFraction), Math.Max(0, _overlay.Bounds.Height)))
            : null;
    }

    private void ApplyTransforms()
    {
        ApplyTransform(_first, _firstBitmap, _firstViewport);
        ApplyTransform(_second, _secondBitmap, _secondViewport);
        var overlayViewport = (Border)_overlay.Children[0]!;
        ApplyTransform(_overlayFirstImage, _firstBitmap, overlayViewport);
        ApplyTransform(_overlaySecondImage, _secondBitmap, overlayViewport);
    }

    private void ApplyTransform(Image image, Bitmap? bitmap, Border viewport)
    {
        if (bitmap is null || viewport.Bounds.Width <= 0 || viewport.Bounds.Height <= 0) return;
        var width = viewport.Bounds.Width;
        var height = viewport.Bounds.Height;
        var baseRect = ComparisonViewport.Fit(width, height, bitmap.PixelSize.Width, bitmap.PixelSize.Height, 1, 0, 0);
        var targetRect = ComparisonViewport.Fit(width, height, bitmap.PixelSize.Width, bitmap.PixelSize.Height,
            _model.Zoom, _model.OffsetX, _model.OffsetY);
        var centeredX = width / 2 + (baseRect.X + baseRect.Width / 2 - width / 2) * _model.Zoom;
        var centeredY = height / 2 + (baseRect.Y + baseRect.Height / 2 - height / 2) * _model.Zoom;
        var transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform(_model.Zoom, _model.Zoom));
        transform.Children.Add(new TranslateTransform(targetRect.X - centeredX, targetRect.Y - centeredY));
        image.RenderTransformOrigin = RelativePoint.Center;
        image.RenderTransform = transform;
    }

    private void RefreshMode()
    {
        _viewports.Children.Clear();
        if (_model.Mode == ComparisonMode.Overlay)
        {
            _viewports.ColumnDefinitions = new ColumnDefinitions("*");
            _viewports.Children.Add(_overlay);
            _viewports.RowDefinitions = new RowDefinitions("*,Auto");
            Grid.SetRow(_overlay, 0);
        }
        else
        {
            _viewports.ColumnDefinitions = new ColumnDefinitions("*,*");
            _viewports.RowDefinitions = new RowDefinitions("*");
            _viewports.Children.Add(_firstPanel);
            Grid.SetColumn(_secondPanel, 1);
            _viewports.Children.Add(_secondPanel);
        }
        UpdateOverlayClip();
    }

    private void OnWheel(object? sender, PointerWheelEventArgs args)
    {
        _model.SetZoom(_model.Zoom * (args.Delta.Y > 0 ? 1.15 : 1 / 1.15));
        args.Handled = true;
    }

    private void OnPanPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_model.Zoom <= 1 || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _panStart = args.GetPosition(this);
        _panStartX = _model.OffsetX;
        _panStartY = _model.OffsetY;
        args.Pointer.Capture((IInputElement)sender!);
    }

    private void OnPanMoved(object? sender, PointerEventArgs args)
    {
        if (_panStart is not { } start) return;
        var current = args.GetPosition(this);
        _model.SetOffset(_panStartX - (current.X - start.X) / Math.Max(1, _viewports.Bounds.Width),
            _panStartY - (current.Y - start.Y) / Math.Max(1, _viewports.Bounds.Height));
    }

    private void OnPanReleased(object? sender, PointerReleasedEventArgs args)
    {
        _panStart = null;
        args.Pointer.Capture(null);
    }
}
