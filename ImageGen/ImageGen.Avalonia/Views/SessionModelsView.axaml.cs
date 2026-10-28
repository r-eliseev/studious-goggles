using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

/// <summary>Image models of one session: search, checkboxes, runs per model, refresh and save.</summary>
public partial class SessionModelsView : UserControl, IAppearing
{
    public SessionModelsView()
    {
        AvaloniaXamlLoader.Load(this);
        DialogButtonOrder.Apply(this.FindControl<Panel>("ButtonRow")!, this.FindControl<Button>("SaveButton")!, this.FindControl<Button>("CancelButton")!);
    }

    public void OnAppearing() => (DataContext as SessionModelsPageModel)?.OnAppearingAsync().FireAndForgetSafeAsync();
}
