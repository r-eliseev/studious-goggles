using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class ModelPickerPage : ContentPage, IQueryAttributable
{
    private readonly ModelPickerPageModel _model;
    private bool _loaded;

    public ModelPickerPage(ModelPickerPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    /// <summary>Shell passes the route query (the session id and the generation id) here before the page appears.</summary>
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
