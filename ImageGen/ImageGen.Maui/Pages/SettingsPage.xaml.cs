using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

/// <summary>Settings: the OpenRouter key (last four characters), the appearance and the logs folder.</summary>
public partial class SettingsPage : ContentPage
{
    private readonly SettingsPageModel _model;

    public SettingsPage(SettingsPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _model.LoadCommand.ExecuteAsync(null).FireAndForgetSafeAsync();
    }

    /// <summary>Radio buttons have no command in MAUI: the checked one sends its theme to the page model.</summary>
    private void OnThemeChecked(object? sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
        {
            return;
        }

        var theme = sender == ThemeSystem ? AppearanceTheme.System
            : sender == ThemeDark ? AppearanceTheme.Dark
            : AppearanceTheme.Light;
        _model.SelectThemeCommand.ExecuteAsync(theme).FireAndForgetSafeAsync();
    }
}
