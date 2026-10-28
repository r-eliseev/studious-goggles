using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

public partial class SessionsView : UserControl, IAppearing
{
    /// <summary>From this width the empty chat state is shown beside the list.</summary>
    private const double WideWidth = 840;

    public SessionsView()
    {
        AvaloniaXamlLoader.Load(this);
        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
    }

    public void OnAppearing()
    {
        if (DataContext is SessionsPageModel model)
        {
            // Opening a session from this page goes to its chat; the chat page is the one that replaces.
            model.ReplaceOnOpen = false;
            model.OnAppearingAsync().FireAndForgetSafeAsync();
        }
    }

    private void ApplyWidth(double width)
    {
        var wide = width >= WideWidth;
        var layout = this.FindControl<Grid>("Layout")!;
        layout.ColumnDefinitions[0].Width = wide ? new GridLength(272) : GridLength.Star;
        layout.ColumnDefinitions[1].Width = wide ? GridLength.Star : new GridLength(0);
        this.FindControl<Border>("EmptyState")!.IsVisible = wide;
    }
}
