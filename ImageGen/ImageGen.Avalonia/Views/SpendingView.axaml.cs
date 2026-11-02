using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

public partial class SpendingView : UserControl, IAppearing
{
    public SpendingView() => AvaloniaXamlLoader.Load(this);

    public void OnAppearing() => (DataContext as SpendingPageModel)?.LoadCommand.ExecuteAsync(null).FireAndForgetSafeAsync();

    private void OnPeriodChanged(object? sender, SelectionChangedEventArgs e) =>
        (DataContext as SpendingPageModel)?.RefreshCommand.ExecuteAsync(null).FireAndForgetSafeAsync();
}
