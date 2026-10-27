using ImageGen.Maui.PageModels;
using ImageGen.Maui.Utilities;

namespace ImageGen.Maui.Pages;

public partial class ChatPage : ContentPage
{
    private readonly ChatPageModel _model;

    public ChatPage(ChatPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _model.OnAppearingAsync().FireAndForgetSafeAsync();
    }

    protected override void OnDisappearing()
    {
        _model.OnDisappearing();
        base.OnDisappearing();
    }
}
