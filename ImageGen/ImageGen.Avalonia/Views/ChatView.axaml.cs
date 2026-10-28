using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

/// <summary>Chat of one session. Enter sends the message; Shift+Enter starts a new line.</summary>
public partial class ChatView : UserControl, IAppearing, IDisappearing
{
    /// <summary>From this width the session list is shown beside the chat.</summary>
    private const double WideWidth = 840;

    public ChatView()
    {
        AvaloniaXamlLoader.Load(this);
        // Tunnel phase: Enter is taken before the TextBox turns it into a new line.
        this.FindControl<TextBox>("Input")?.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
    }

    /// <summary>The session list of the wide layout; opening a session in it replaces this chat.</summary>
    public SessionsPageModel? SessionPicker
    {
        get => this.FindControl<SessionsListView>("SessionList")?.DataContext as SessionsPageModel;
        set => this.FindControl<SessionsListView>("SessionList")!.DataContext = value;
    }

    public void OnAppearing()
    {
        if (SessionPicker is { } picker)
        {
            picker.ReplaceOnOpen = true;
            picker.OnAppearingAsync().FireAndForgetSafeAsync();
        }

        if (DataContext is not ChatPageModel model)
        {
            return;
        }

        model.Items.CollectionChanged -= OnItemsChanged;
        model.Items.CollectionChanged += OnItemsChanged;
        model.OnAppearingAsync().FireAndForgetSafeAsync();
    }

    public void OnDisappearing()
    {
        if (DataContext is not ChatPageModel model)
        {
            return;
        }

        model.Items.CollectionChanged -= OnItemsChanged;
        model.OnDisappearing();
    }

    /// <summary>The "More" icon opens the same context menu as a right-click on the card.</summary>
    private void OnMoreClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        var card = control.GetVisualAncestors().OfType<Border>()
            .FirstOrDefault(border => border.Classes.Contains("card"));
        if (card?.ContextMenu is { } menu)
        {
            menu.Open(card);
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        // Handled even when nothing is sent: an empty or busy input must not add a new line either.
        e.Handled = true;
        if (DataContext is ChatPageModel model && model.SendCommand.CanExecute(null))
        {
            model.SendCommand.Execute(null);
        }
    }

    /// <summary>New rows go to the bottom of the transcript; the scroll waits until the layout has the new row.</summary>
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(
            () => this.FindControl<ScrollViewer>("Transcript")?.ScrollToEnd(),
            DispatcherPriority.Background);

    private void ApplyWidth(double width)
    {
        var wide = width >= WideWidth;
        var layout = this.FindControl<Grid>("Layout")!;
        layout.ColumnDefinitions[0].Width = wide ? new GridLength(272) : new GridLength(0);
        this.FindControl<SessionsListView>("SessionList")!.IsVisible = wide;
        if (DataContext is ChatPageModel model)
        {
            // A narrow window opens the models and parameters as a screen; any open pane is closed.
            model.IsWide = wide;
            if (!wide)
            {
                model.ClosePaneCommand.Execute(null);
            }
        }
    }
}
