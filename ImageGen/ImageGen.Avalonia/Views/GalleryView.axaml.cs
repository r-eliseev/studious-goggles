using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

public partial class GalleryView : UserControl, IAppearing, IDisappearing
{
    private bool _loading;
    public GalleryView() => AvaloniaXamlLoader.Load(this);

    public void OnAppearing() => LoadAsync().FireAndForgetSafeAsync();

    public void OnDisappearing() => (DataContext as GalleryPageModel)?.Dispose();

    private async Task LoadAsync()
    {
        if (DataContext is not GalleryPageModel model) return;
        _loading = true;
        try
        {
            await model.LoadCommand.ExecuteAsync(null);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading) (DataContext as GalleryPageModel)?.ApplyFiltersCommand.ExecuteAsync(null).FireAndForgetSafeAsync();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ListBox list && e.OffsetDelta.Y > 0 && e.ExtentDelta.Y == 0)
        {
            var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scroll is not null && scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y < 160)
            {
                (DataContext as GalleryPageModel)?.LoadMoreCommand.ExecuteAsync(null).FireAndForgetSafeAsync();
            }
        }
    }
}
