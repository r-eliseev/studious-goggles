using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class SpendingPage : ContentPage
{
    private readonly SpendingPageModel _model;

    public SpendingPage(SpendingPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _model.LoadCommand.ExecuteAsync(null).FireAndForgetSafeAsync();
    }

    private void OnPeriodChanged(object? sender, EventArgs e) =>
        _model.RefreshCommand.ExecuteAsync(null).FireAndForgetSafeAsync();
}
