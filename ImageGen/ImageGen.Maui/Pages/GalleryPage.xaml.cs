using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class GalleryPage : ContentPage
{
    private readonly GalleryPageModel _model;
    private bool _loading;

    public GalleryPage(GalleryPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadAsync().FireAndForgetSafeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _model.Dispose();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            await _model.LoadCommand.ExecuteAsync(null);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        if (!_loading) _model.ApplyFiltersCommand.ExecuteAsync(null).FireAndForgetSafeAsync();
    }

    private void OnItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView view || e.CurrentSelection.FirstOrDefault() is not GalleryItemViewModel item) return;
        view.SelectedItem = null;
        _model.OpenCommand.ExecuteAsync(item).FireAndForgetSafeAsync();
    }

    private void OnRemainingItemsThresholdReached(object? sender, EventArgs e) =>
        _model.LoadMoreCommand.ExecuteAsync(null).FireAndForgetSafeAsync();
}
