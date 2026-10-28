using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class ChatPage : ContentPage, IQueryAttributable
{
    private readonly ChatPageModel _model;

    public ChatPage(ChatPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    /// <summary>Shell passes the route query (the session id) here before the page appears.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query) => _model.ApplyQueryAttributes(query);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _model.OnAppearingAsync().FireAndForgetSafeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _model.OnDisappearing();
    }
}
