using System.ComponentModel;
using ImageGen.Presentation.PageModels;

namespace ImageGen.Maui.Pages;

public partial class ApiKeyPage : ContentPage
{
    private readonly ApiKeyPageModel _model;

    public ApiKeyPage(ApiKeyPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
        _model.PropertyChanged += OnModelPropertyChanged;
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApiKeyPageModel.StatusText) && !string.IsNullOrEmpty(_model.StatusText))
        {
            SemanticScreenReader.Announce(_model.StatusText);
        }
    }
}
