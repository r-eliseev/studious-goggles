using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Views;

public partial class MainView : UserControl
{
    private TopLevel? _attachedTopLevel;
    private IInputPane? _inputPane;

    public MainView()
    {
        // InitializeComponent also assigns the named controls; on iOS AvaloniaXamlLoader.Load alone left PageHost null.
        InitializeComponent();
        PageHost.PropertyChanged += (_, args) =>
        {
            if (args.Property == ContentControl.ContentProperty && PageHost.Content is Control page)
            {
                MobileTweaks.Apply(page);
            }
        };

        Current = this;
        var navigation = MobileApp.Services.GetRequiredService<AvaloniaNavigationService>();
        navigation.AttachHost(PageHost);
        if (!MobileApp.Started)
        {
            MobileApp.Started = true;
            MobileApp.Services.GetRequiredService<INavigationService>()
                .GoToAsync(Routes.Sessions)
                .FireAndForgetSafeAsync();
        }

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    public static MainView? Current { get; private set; }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        _attachedTopLevel = TopLevel.GetTopLevel(this);
        if (_attachedTopLevel is null)
        {
            return;
        }

        _attachedTopLevel.BackRequested += OnBackRequested;
        _inputPane = _attachedTopLevel.InputPane;
        if (_inputPane is not null)
        {
            _inputPane.StateChanged += OnInputPaneStateChanged;
            UpdateInputMargin(_inputPane);
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_attachedTopLevel is not null)
        {
            _attachedTopLevel.BackRequested -= OnBackRequested;
            _attachedTopLevel = null;
        }

        if (_inputPane is not null)
        {
            _inputPane.StateChanged -= OnInputPaneStateChanged;
            _inputPane = null;
        }

        PageHost.Margin = default;
        var services = MobileApp.Services;
        while (services.GetRequiredService<OverlayDialogService>().TryCloseTop())
        {
        }

        while (services.GetRequiredService<OverlayImageViewerService>().TryCloseTop())
        {
        }

        while (services.GetRequiredService<OverlayImageComparisonService>().TryCloseTop())
        {
        }

        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    private void OnBackRequested(object? sender, RoutedEventArgs args)
    {
        var services = MobileApp.Services;
        if (services.GetRequiredService<OverlayDialogService>().TryCloseTop()
            || services.GetRequiredService<OverlayImageViewerService>().TryCloseTop()
            || services.GetRequiredService<OverlayImageComparisonService>().TryCloseTop())
        {
            args.Handled = true;
            return;
        }

        var navigation = services.GetRequiredService<AvaloniaNavigationService>();
        if (navigation.CurrentRoute != Routes.Sessions)
        {
            navigation.GoBackAsync().FireAndForgetSafeAsync();
            args.Handled = true;
        }
    }

    private void OnInputPaneStateChanged(object? sender, EventArgs args)
    {
        if (sender is IInputPane pane)
        {
            UpdateInputMargin(pane);
        }
    }

    private void UpdateInputMargin(IInputPane pane) =>
        PageHost.Margin = new Thickness(0, 0, 0, pane.State == InputPaneState.Open ? pane.OccludedRect.Height : 0);
}
