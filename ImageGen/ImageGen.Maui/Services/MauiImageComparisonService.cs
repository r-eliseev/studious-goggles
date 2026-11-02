using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;
using Microsoft.Maui.Controls.Shapes;

namespace ImageGen.Maui.Services;

/// <summary>Shows two saved images in a modal page and returns after the page closes.</summary>
public sealed class MauiImageComparisonService : IImageComparisonService
{
    private int _isOpen;

    public async Task ShowAsync(ComparisonPageModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (Interlocked.Exchange(ref _isOpen, 1) != 0) return;
        var page = new MauiImageComparisonPage(model);
        using var registration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(page.Close));
        try
        {
            await Shell.Current.Navigation.PushModalAsync(page);
            await page.Closed;
        }
        finally
        {
            if (Shell.Current.Navigation.ModalStack.LastOrDefault() == page)
            {
                await Shell.Current.Navigation.PopModalAsync();
            }
            Interlocked.Exchange(ref _isOpen, 0);
        }
    }
}

internal sealed class MauiImageComparisonPage : ContentPage
{
    private readonly ComparisonPageModel _model;
    private readonly Image _first = new() { Aspect = Aspect.AspectFit };
    private readonly Image _second = new() { Aspect = Aspect.AspectFit };
    private readonly Image _overlayFirst = new() { Aspect = Aspect.AspectFit };
    private readonly Image _overlaySecond = new() { Aspect = Aspect.AspectFit };
    private readonly Grid _viewports = new();
    private Grid _sideBySide = null!;
    private Grid _overlay = null!;
    private View _sideFirstPanel = null!;
    private View _sideSecondPanel = null!;
    private readonly Slider _slider = new() { Minimum = 0, Maximum = 1, Value = 0.5 };
    private readonly Slider _zoom = new() { Minimum = 1, Maximum = 8, Value = 1 };
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private double _panX;
    private double _panY;
    private double _startX;
    private bool _closing;

    public MauiImageComparisonPage(ComparisonPageModel model)
    {
        _model = model;
        BackgroundColor = Colors.Black;
        _first.Source = ImageSource.FromFile(model.First.AbsolutePath);
        _second.Source = ImageSource.FromFile(model.Second.AbsolutePath);
        _overlayFirst.Source = ImageSource.FromFile(model.First.AbsolutePath);
        _overlaySecond.Source = ImageSource.FromFile(model.Second.AbsolutePath);
        _sideBySide = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) } };
        _sideFirstPanel = ImagePanel(_first, model.First);
        _sideSecondPanel = ImagePanel(_second, model.Second);
        _sideBySide.Add(_sideFirstPanel, 0, 0);
        _sideBySide.Add(_sideSecondPanel, 1, 0);
        var overlayArea = new Grid();
        overlayArea.Add(_overlayFirst);
        overlayArea.Add(_overlaySecond);
        overlayArea.SizeChanged += (_, _) => UpdateOverlayClip();
        _overlay = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        _overlay.Add(overlayArea, 0, 0);
        var overlayMetadata = new Label { Text = string.Join(" · ", model.First.ModelText, model.First.SeedText, model.First.CostText, "⇄", model.Second.ModelText, model.Second.SeedText, model.Second.CostText), TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center };
        _overlay.Add(overlayMetadata, 0, 1);
        _viewports.Children.Add(_sideBySide);
        _viewports.Children.Add(_overlay);
        _overlay.IsVisible = false;
        var sideBySide = MakeButton(ImageGen.Presentation.Resources.Strings.AppResources.ComparisonSideBySide, () => _model.Mode = ComparisonMode.SideBySide);
        var overlay = MakeButton(ImageGen.Presentation.Resources.Strings.AppResources.ComparisonOverlay, () => _model.Mode = ComparisonMode.Overlay);
        var close = MakeButton(ImageGen.Presentation.Resources.Strings.AppResources.ComparisonClose, Close);
        var sliderLabel = new Label { Text = ImageGen.Presentation.Resources.Strings.AppResources.ComparisonZoom, TextColor = Colors.White, VerticalTextAlignment = TextAlignment.Center };
        var controls = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
        controls.Add(sideBySide);
        controls.Add(overlay);
        controls.Add(_slider);
        controls.Add(sliderLabel);
        controls.Add(_zoom);
        controls.Add(close);
        var root = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }, Padding = 12 };
        root.Add(_viewports, 0, 0);
        root.Add(controls, 0, 1);
        Content = root;
        _slider.ValueChanged += (_, args) => _model.SliderFraction = args.NewValue;
        _zoom.ValueChanged += (_, args) => { _model.SetZoom(args.NewValue); ApplyGestureTransform(); };
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinchUpdated;
        _viewports.GestureRecognizers.Add(pinch);
        var pan = new PanGestureRecognizer { TouchPoints = 1 };
        pan.PanUpdated += OnPanUpdated;
        _viewports.GestureRecognizers.Add(pan);
        _model.PropertyChanged += OnModelChanged;
    }

    public Task Closed => _closed.Task;

    public void Close()
    {
        if (_closing) return;
        _closing = true;
        _model.PropertyChanged -= OnModelChanged;
        _first.Source = null;
        _second.Source = null;
        _overlayFirst.Source = null;
        _overlaySecond.Source = null;
        _closed.TrySetResult();
    }

    protected override bool OnBackButtonPressed() { Close(); return true; }

    private static View ImagePanel(Image image, ComparisonImage data)
    {
        var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        var frame = new Border { BackgroundColor = Colors.Black, StrokeThickness = 0, Content = image };
        grid.Add(frame, 0, 0);
        var metadata = string.Join(" · ", data.ModelText, data.SeedText, data.CostText);
        var label = new Label { Text = metadata, TextColor = Colors.White, LineBreakMode = LineBreakMode.WordWrap };
        grid.Add(label, 0, 1);
        return grid;
    }

    private static Button MakeButton(string title, Action action)
    {
        var button = new Button { Text = title, MinimumWidthRequest = 48, MinimumHeightRequest = 48 };
        SemanticProperties.SetDescription(button, title);
        button.Clicked += (_, _) => action();
        return button;
    }

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ComparisonPageModel.Mode))
        {
            _sideBySide.IsVisible = _model.Mode == ComparisonMode.SideBySide;
            _overlay.IsVisible = _model.Mode == ComparisonMode.Overlay;
        }
        if (args.PropertyName == nameof(ComparisonPageModel.SliderFraction)) UpdateOverlayClip();
        if (args.PropertyName is nameof(ComparisonPageModel.Zoom) or nameof(ComparisonPageModel.OffsetX) or nameof(ComparisonPageModel.OffsetY)) ApplyGestureTransform();
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs args)
    {
        if (args.Status == GestureStatus.Started) _startX = _model.Zoom;
        else if (args.Status == GestureStatus.Running) _model.SetZoom(_startX * args.Scale);
        _zoom.Value = _model.Zoom;
        ApplyGestureTransform();
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs args)
    {
        if (args.StatusType == GestureStatus.Started) { _panX = _model.OffsetX; _panY = _model.OffsetY; }
        else if (args.StatusType == GestureStatus.Running && _model.Zoom > 1)
        {
            _model.SetOffset(_panX + args.TotalX / Math.Max(1, _viewports.Width), _panY + args.TotalY / Math.Max(1, _viewports.Height));
            ApplyGestureTransform();
        }
    }

    private void ApplyGestureTransform()
    {
        var scale = _model.Zoom;
        var tx = _model.OffsetX * _viewports.Width;
        var ty = _model.OffsetY * _viewports.Height;
        _first.Scale = scale;
        _second.Scale = scale;
        _first.TranslationX = tx;
        _second.TranslationX = tx;
        _first.TranslationY = ty;
        _second.TranslationY = ty;
        _overlayFirst.Scale = scale;
        _overlaySecond.Scale = scale;
        _overlayFirst.TranslationX = tx;
        _overlaySecond.TranslationX = tx;
        _overlayFirst.TranslationY = ty;
        _overlaySecond.TranslationY = ty;
    }

    private void UpdateOverlayClip()
    {
        _overlaySecond.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, _overlay.Width * _model.SliderFraction), Math.Max(0, _overlay.Height)));
    }
}
