using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class SessionsPage : ContentPage
{
    private readonly SessionsPageModel _model;

    public SessionsPage(SessionsPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _model.OnAppearingAsync().FireAndForgetSafeAsync();
    }
}
