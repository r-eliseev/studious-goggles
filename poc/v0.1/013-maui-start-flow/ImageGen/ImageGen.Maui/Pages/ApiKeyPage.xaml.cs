using ImageGen.Maui.PageModels;

namespace ImageGen.Maui.Pages;

public partial class ApiKeyPage : ContentPage
{
    public ApiKeyPage(ApiKeyPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
