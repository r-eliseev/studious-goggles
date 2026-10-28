using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

/// <summary>Parameters of one session: aspect ratio, resolution, output format and quality.</summary>
public partial class SessionParametersView : UserControl, IAppearing
{
    public SessionParametersView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void OnAppearing() => (DataContext as SessionParametersPageModel)?.OnAppearingAsync().FireAndForgetSafeAsync();
}
