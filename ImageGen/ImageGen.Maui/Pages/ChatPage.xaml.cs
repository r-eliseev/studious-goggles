using System.Collections.Specialized;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class ChatPage : ContentPage, IQueryAttributable
{
    /// <summary>From this width the session list is shown beside the chat.</summary>
    private const double WideWidth = 840;

    private readonly ChatPageModel _model;
    private readonly SessionsPageModel _sessions;
    private readonly TranscriptFollower _follower = new();
    private bool _scrollPending;
    private bool _programmaticScroll;
    private bool _active;

    public ChatPage(ChatPageModel model, SessionsPageModel sessions)
    {
        InitializeComponent();
        BindingContext = _model = model;
        _sessions = sessions;
        SessionList.BindingContext = sessions;
        SizeChanged += (_, _) => ApplyWidth(Width);
    }

    private void ApplyWidth(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var wide = width >= WideWidth;
        ChatLayout.ColumnDefinitions[0].Width = wide ? new GridLength(272) : new GridLength(0);
        SessionList.IsVisible = wide;
        // A narrow window opens the models and parameters as a screen; any open pane is closed.
        _model.IsWide = wide;
        if (!wide)
        {
            _model.ClosePaneCommand.Execute(null);
        }
    }

    /// <summary>Shell passes the route query (the session id) here before the page appears.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query) => _model.ApplyQueryAttributes(query);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _active = true;
        _follower.RowsChanged();
        _model.Items.CollectionChanged -= OnItemsChanged;
        _model.Items.CollectionChanged += OnItemsChanged;
        _model.CardUpdated -= OnCardUpdated;
        _model.CardUpdated += OnCardUpdated;
        Transcript.Scrolled -= OnTranscriptScrolled;
        Transcript.Scrolled += OnTranscriptScrolled;
        Transcript.SizeChanged -= OnTranscriptSizeChanged;
        Transcript.SizeChanged += OnTranscriptSizeChanged;
        // Opening a session in the list replaces this chat, so going back returns to the list below it.
        _sessions.ReplaceOnOpen = true;
        _sessions.OnAppearingAsync().FireAndForgetSafeAsync();
        _model.OnAppearingAsync().FireAndForgetSafeAsync();
        ScrollToEndLater();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _active = false;
        _model.Items.CollectionChanged -= OnItemsChanged;
        _model.CardUpdated -= OnCardUpdated;
        Transcript.Scrolled -= OnTranscriptScrolled;
        Transcript.SizeChanged -= OnTranscriptSizeChanged;
        _model.OnDisappearing();
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

    private void OnTranscriptSizeChanged(object? sender, EventArgs e)
    {
        if (_follower.ViewportChanged())
        {
            ScrollToEndLater();
        }
    }

    private void OnTranscriptScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (_programmaticScroll || _scrollPending || e.VerticalDelta == 0)
        {
            return;
        }

        if (e.VerticalDelta < 0)
        {
            _follower.UserScrolled(false);
            return;
        }

        if (e.LastVisibleItemIndex >= _model.Items.Count - 1)
        {
            _follower.UserScrolled(true);
            ScrollToEndLater();
        }
    }

    private void OnTranscriptRowSizeChanged(object? sender, EventArgs e)
    {
        if (_active && _follower.ContentChanged())
        {
            ScrollToEndLater();
        }
    }

    private void ScrollToEndLater()
    {
        if (_scrollPending)
        {
            return;
        }

        _scrollPending = true;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                if (!_active || !_follower.IsFollowing || _model.Items.Count == 0)
                {
                    return;
                }

                _programmaticScroll = true;
                Transcript.ScrollTo(_model.Items.Count - 1, position: ScrollToPosition.End, animate: false);
                Dispatcher.Dispatch(() => _programmaticScroll = false);
            }
            finally
            {
                _scrollPending = false;
            }
        });
    }
}
