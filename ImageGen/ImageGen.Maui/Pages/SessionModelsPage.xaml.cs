using ImageGen.Maui.PageModels;
using ImageGen.Maui.Utilities;

namespace ImageGen.Maui.Pages;

public partial class SessionModelsPage : ContentPage, IQueryAttributable
{
    private readonly SessionModelsPageModel _model;
    private bool _loaded;

    public SessionModelsPage(SessionModelsPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    /// <summary>Shell passes the route query (the session id) here before the page appears.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query) => _model.ApplyQueryAttributes(query);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _model.OnAppearingAsync().FireAndForgetSafeAsync();
    }
}
