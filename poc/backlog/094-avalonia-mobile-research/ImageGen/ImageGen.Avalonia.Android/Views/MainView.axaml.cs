using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using ImageGen.Avalonia.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        AvaloniaXamlLoader.Load(this);
        PageArea = this.FindControl<ContentControl>("PageHost")!;
        DialogArea = this.FindControl<Grid>("DialogLayer")!;
        PageArea.PropertyChanged += (_, args) =>
        {
            if (args.Property == ContentControl.ContentProperty && PageArea.Content is Control page)
            {
                MobileTweaks.Apply(page);
            }
        };
        var navigation = MobileApp.Services.GetRequiredService<AvaloniaNavigationService>();
        navigation.AttachHost(PageArea);
        if (!MobileApp.Started)
        {
            MobileApp.Started = true;
            MobileApp.Services.GetRequiredService<ImageGen.Presentation.Services.INavigationService>()
                .GoToAsync(ImageGen.Presentation.Routes.Sessions);
        }
        CurrentView = this;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }
    public ContentControl PageArea { get; }
    public Grid DialogArea { get; }
    public static MainView? CurrentView { get; set; }

    private void OnAttachedToVisualTree(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs args)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        top.BackRequested += OnBackRequested;
        if (top.InputPane is { } pane)
        {
            pane.StateChanged += OnInputPaneStateChanged;
            UpdateInputMargin(pane);
        }
        else
        {
            global::Android.Util.Log.Info("ImageGenProbe", "InputPane unavailable");
        }
    }

    private void OnDetachedFromVisualTree(object? sender, global::Avalonia.VisualTreeAttachmentEventArgs args)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        top.BackRequested -= OnBackRequested;
        if (top.InputPane is { } pane) pane.StateChanged -= OnInputPaneStateChanged;
    }

    private void OnBackRequested(object? sender, RoutedEventArgs args)
    {
        if (DialogArea.IsVisible)
        {
            ImageGen.Avalonia.Platform.OverlayDialogService.CancelOpenDialog();
            args.Handled = true;
            return;
        }

        var navigation = MobileApp.Services.GetRequiredService<AvaloniaNavigationService>();
        if (navigation.CurrentRoute != ImageGen.Presentation.Routes.Sessions)
        {
            navigation.GoBackAsync();
            args.Handled = true;
        }
    }

    private void OnInputPaneStateChanged(object? sender, EventArgs args)
    {
        if (sender is IInputPane pane) UpdateInputMargin(pane);
    }

    private void UpdateInputMargin(IInputPane pane) =>
        PageArea.Margin = new Thickness(0, 0, 0, pane.State == InputPaneState.Open ? pane.OccludedRect.Height : 0);
}
