using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Title = AppResources.AppTitle;
    }

    /// <summary>Area where pages are shown: the navigation service puts the current page here.</summary>
    public ContentControl PageArea => this.FindControl<ContentControl>("PageHost")!;
}
