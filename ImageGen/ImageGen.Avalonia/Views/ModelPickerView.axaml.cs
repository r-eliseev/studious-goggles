using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

/// <summary>Picks a model to run one generation on: the used models on top, the whole catalog under "All models" (task 076).</summary>
public partial class ModelPickerView : UserControl, IAppearing
{
    public ModelPickerView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void OnAppearing() => (DataContext as ModelPickerPageModel)?.OnAppearingAsync().FireAndForgetSafeAsync();
}
