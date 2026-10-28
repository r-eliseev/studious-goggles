using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class SessionsPage : ContentPage
{
    /// <summary>From this width the empty chat state is shown beside the list.</summary>
    private const double WideWidth = 840;

    private readonly SessionsPageModel _model;

    public SessionsPage(SessionsPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
        SizeChanged += (_, _) => ApplyWidth(Width);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _model.OnAppearingAsync().FireAndForgetSafeAsync();
    }

    private void ApplyWidth(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var wide = width >= WideWidth;
        SessionsLayout.ColumnDefinitions[0].Width = wide ? new GridLength(272) : GridLength.Star;
        SessionsLayout.ColumnDefinitions[1].Width = wide ? GridLength.Star : new GridLength(0);
        EmptyState.IsVisible = wide;
    }
}
