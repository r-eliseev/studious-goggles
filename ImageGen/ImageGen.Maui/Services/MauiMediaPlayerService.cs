using CommunityToolkit.Maui.Views;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Services;

/// <summary>Shows one media file in a modal page with a single native player.</summary>
public sealed class MauiMediaPlayerService : IMediaPlayerService
{
    private int _isOpen;

    public bool IsSupported => true;

    public async Task ShowAsync(string filePath, string accessibleLabel, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (Interlocked.Exchange(ref _isOpen, 1) != 0) return;
        var page = new MauiMediaPlayerPage(filePath, accessibleLabel);
        using var registration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(page.Close));
        try
        {
            await Shell.Current.Navigation.PushModalAsync(page);
            await page.Closed;
        }
        finally
        {
            if (Shell.Current.Navigation.ModalStack.LastOrDefault() == page)
                await Shell.Current.Navigation.PopModalAsync();
            Interlocked.Exchange(ref _isOpen, 0);
        }
    }
}

internal sealed class MauiMediaPlayerPage : ContentPage
{
    private readonly MediaElement _player;
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closing;
#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement? _nativeView;
#endif

    public MauiMediaPlayerPage(string filePath, string accessibleLabel)
    {
        BackgroundColor = Colors.Black;
        _player = new MediaElement
        {
            Source = MediaSource.FromFile(filePath),
            Aspect = Aspect.AspectFit,
            ShouldAutoPlay = true,
            ShouldShowPlaybackControls = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        SemanticProperties.SetDescription(_player, accessibleLabel);
        var close = new Button { Text = AppResources.ImageViewerClose, MinimumWidthRequest = 48, MinimumHeightRequest = 48, Padding = 8 };
        SemanticProperties.SetDescription(close, AppResources.ImageViewerClose);
        close.Clicked += (_, _) => Close();
        close.HorizontalOptions = LayoutOptions.End;
        close.VerticalOptions = LayoutOptions.Start;
        var root = new Grid { Padding = 12 };
        root.Add(_player);
        root.Add(close);
        Content = root;
    }

    public Task Closed => _closed.Task;

    protected override bool OnBackButtonPressed()
    {
        Close();
        return true;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if WINDOWS
        if (_nativeView is not null) _nativeView.KeyDown -= OnWindowsKeyDown;
        _nativeView = Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
        if (_nativeView is not null) _nativeView.KeyDown += OnWindowsKeyDown;
#endif
    }

#if WINDOWS
    private void OnWindowsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key != Windows.System.VirtualKey.Escape) return;
        args.Handled = true;
        Close();
    }
#endif

    public void Close()
    {
        if (_closing) return;
        _closing = true;
        _player.Stop();
        _player.Handler?.DisconnectHandler();
#if WINDOWS
        if (_nativeView is not null) _nativeView.KeyDown -= OnWindowsKeyDown;
        _nativeView = null;
#endif
        _closed.TrySetResult();
    }
}
