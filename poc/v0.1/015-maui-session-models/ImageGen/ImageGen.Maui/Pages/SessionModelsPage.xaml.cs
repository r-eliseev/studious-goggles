using ImageGen.Maui.PageModels;
using ImageGen.Maui.Utilities;

namespace ImageGen.Maui.Pages;

public partial class SessionModelsPage : ContentPage
{
    private readonly SessionModelsPageModel _model;
    private bool _loaded;

    public SessionModelsPage(SessionModelsPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

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
