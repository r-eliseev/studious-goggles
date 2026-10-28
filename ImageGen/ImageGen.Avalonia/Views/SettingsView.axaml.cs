using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

/// <summary>Settings: the OpenRouter key (last four characters), the appearance and the logs folder.</summary>
public partial class SettingsView : UserControl, IAppearing
{
    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void OnAppearing() => (DataContext as SettingsPageModel)?.LoadCommand.ExecuteAsync(null).FireAndForgetSafeAsync();
}
