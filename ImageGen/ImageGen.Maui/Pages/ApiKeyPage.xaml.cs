using System.ComponentModel;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class ApiKeyPage : ContentPage, IQueryAttributable
{
    private readonly ApiKeyPageModel _model;

    public ApiKeyPage(ApiKeyPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
        _model.PropertyChanged += OnModelPropertyChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _model.ApplyQueryAttributes(query);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _model.OnAppearingAsync().FireAndForgetSafeAsync();
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApiKeyPageModel.StatusText) && !string.IsNullOrEmpty(_model.StatusText))
        {
            SemanticScreenReader.Announce(_model.StatusText);
        }
    }
}
