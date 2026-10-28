using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class SessionParametersPage : ContentPage, IQueryAttributable
{
    private readonly SessionParametersPageModel _model;
    private bool _loaded;

    public SessionParametersPage(SessionParametersPageModel model)
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
