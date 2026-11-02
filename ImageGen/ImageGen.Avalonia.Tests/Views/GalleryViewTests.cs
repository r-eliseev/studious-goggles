using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;

namespace ImageGen.Avalonia.Tests.Views;

public sealed class GalleryViewTests
{
    [AvaloniaFact]
    public async Task GalleryRoute_LoadsVirtualizedGalleryPage()
    {
        using var shell = new ShellHarness();
        await shell.InitializeDatabaseAsync();
        shell.Window.Show();

        await shell.Navigation.GoToAsync(Routes.Gallery);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is GalleryView view
            && view.DataContext is GalleryPageModel model && !model.IsBusy, "галерея загружена");
        shell.Window.UpdateLayout();

        var page = Assert.IsType<GalleryView>(shell.Window.PageArea.Content);
        Assert.Single(page.GetVisualDescendants().OfType<ListBox>());
        Assert.NotNull(page.DataContext);
    }
}
