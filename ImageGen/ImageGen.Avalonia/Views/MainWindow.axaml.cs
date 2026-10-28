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
}
