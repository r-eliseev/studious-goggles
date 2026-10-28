using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

public partial class SessionsView : UserControl, IAppearing
{
    public SessionsView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void OnAppearing() => (DataContext as SessionsPageModel)?.OnAppearingAsync().FireAndForgetSafeAsync();
}
