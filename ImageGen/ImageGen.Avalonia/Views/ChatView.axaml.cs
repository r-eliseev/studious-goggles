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

    private readonly TranscriptFollower _follower = new();
    private ChatPageModel? _subscribed;
    private bool _active;
    private bool _scrollPending;
    private bool _programmaticScroll;

    public ChatView()
    {
        AvaloniaXamlLoader.Load(this);
        // Tunnel phase: Enter is taken before the TextBox turns it into a new line.
        this.FindControl<TextBox>("Input")?.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        this.FindControl<WrapPanel>("ChatHeader")!.SizeChanged += OnHeaderSizeChanged;
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

        if (_subscribed is { } previous && previous != model)
        {
            previous.Items.CollectionChanged -= OnItemsChanged;
            previous.CardUpdated -= OnCardUpdated;
            previous.OnDisappearing();
        }

        var transcript = this.FindControl<ScrollViewer>("Transcript");
        if (transcript is not null)
        {
            transcript.ScrollChanged -= OnTranscriptScrollChanged;
            transcript.ScrollChanged += OnTranscriptScrollChanged;
            transcript.SizeChanged -= OnTranscriptSizeChanged;
            transcript.SizeChanged += OnTranscriptSizeChanged;
        }

        _subscribed = model;
        _active = true;
        model.Items.CollectionChanged -= OnItemsChanged;
        model.Items.CollectionChanged += OnItemsChanged;
        model.CardUpdated -= OnCardUpdated;
        model.CardUpdated += OnCardUpdated;
        _follower.RowsChanged();
        ScrollToEndLater();
        model.OnAppearingAsync().FireAndForgetSafeAsync();
    }

    public void OnDisappearing()
    {
        _active = false;
        if (_subscribed is { } model)
        {
            model.Items.CollectionChanged -= OnItemsChanged;
            model.CardUpdated -= OnCardUpdated;
            model.OnDisappearing();
        }

        _subscribed = null;
        if (this.FindControl<ScrollViewer>("Transcript") is { } transcript)
        {
            transcript.ScrollChanged -= OnTranscriptScrollChanged;
            transcript.SizeChanged -= OnTranscriptSizeChanged;
        }
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

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_follower.RowsChanged())
        {
            ScrollToEndLater();
        }
    }

    private void OnCardUpdated(object? sender, EventArgs e)
    {
        if (_follower.ContentChanged())
        {
            ScrollToEndLater();
        }
    }

    private void OnTranscriptSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_follower.ViewportChanged())
        {
            ScrollToEndLater();
        }
    }

    private void OnTranscriptScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer transcript)
        {
            return;
        }

        if (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0)
        {
            if (_follower.ViewportChanged())
            {
                ScrollToEndLater();
            }

            return;
        }

        if (_programmaticScroll || _scrollPending || e.OffsetDelta.Y == 0)
        {
            return;
        }

        _follower.UserScrolled(TranscriptFollower.IsAtEnd(transcript.Offset.Y, transcript.Viewport.Height, transcript.Extent.Height));
    }

    /// <summary>Coalesces scrolls until the transcript has laid out the latest content.</summary>
    private void ScrollToEndLater()
    {
        if (_scrollPending)
        {
            return;
        }

        _scrollPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (!_active || !_follower.IsFollowing)
                {
                    return;
                }

                var transcript = this.FindControl<ScrollViewer>("Transcript");
                if (transcript is null)
                {
                    return;
                }

                _programmaticScroll = true;
                transcript.ScrollToEnd();
                Dispatcher.UIThread.Post(() => _programmaticScroll = false, DispatcherPriority.Background);
            }
            finally
            {
                _scrollPending = false;
            }
        }, DispatcherPriority.Background);
    }

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

    private void OnHeaderSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var header = (WrapPanel)sender!;
        var title = this.FindControl<TextBlock>("ChatTitle")!;
        var assistant = this.FindControl<TextBlock>("AssistantChipText")!;
        var buttons = header.Children.OfType<Button>().ToArray();
        title.MaxWidth = Math.Max(0, e.NewSize.Width - 8);

        // Keep short titles and all chips on one row when possible; allocate the remaining width to the assistant label.
        var fixedWidth = title.DesiredSize.Width + buttons.Sum(button => button.DesiredSize.Width) + 40;
        assistant.MaxWidth = Math.Max(0, e.NewSize.Width - fixedWidth);
    }
}
