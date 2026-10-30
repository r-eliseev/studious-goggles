using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class ChatPage : ContentPage, IQueryAttributable
{
    /// <summary>From this width the session list is shown beside the chat.</summary>
    private const double WideWidth = 840;

    private readonly ChatPageModel _model;
    private readonly SessionsPageModel _sessions;

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
        // Opening a session in the list replaces this chat, so going back returns to the list below it.
        _sessions.ReplaceOnOpen = true;
        _sessions.OnAppearingAsync().FireAndForgetSafeAsync();
        _model.OnAppearingAsync().FireAndForgetSafeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _model.OnDisappearing();
    }
}
