using ImageGen.Maui.Resources.Themes;
using ImageGen.Presentation.Appearance;

namespace ImageGen.Maui.Services;

/// <summary>
/// Applies the appearance settings to the running app: swaps the theme dictionary (style and light or dark variant)
/// and sets the app theme. Screens bind to the <c>Ig*</c> keys with <c>DynamicResource</c>, so the change shows
/// without a restart. The system theme is followed when the setting is System.
/// </summary>
public sealed class MauiAppearanceApplier
{
    private readonly AppearanceService _service;
    private ResourceDictionary? _themeDictionary;

    public MauiAppearanceApplier(AppearanceService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>Applies the saved settings now and follows later changes and system theme changes.</summary>
    public void Start(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        Apply(application, _service.Current);
        _service.Changed += settings => MainThread.BeginInvokeOnMainThread(() => Apply(application, settings));
        application.RequestedThemeChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => Apply(application, _service.Current));
    }

    public void Apply(Application application, AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(settings);
        application.UserAppTheme = settings.Theme switch
        {
            AppearanceTheme.Light => AppTheme.Light,
            AppearanceTheme.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };

        var isDark = ThemeSelection.IsDark(settings.Theme, application.PlatformAppTheme == AppTheme.Dark);
        var dictionary = Create(ThemeSelection.DictionaryName(settings.Style, isDark));
        if (_themeDictionary is not null)
        {
            application.Resources.MergedDictionaries.Remove(_themeDictionary);
        }

        application.Resources.MergedDictionaries.Add(dictionary);
        _themeDictionary = dictionary;
    }

    private static ResourceDictionary Create(string name) => name switch
    {
        "QuietLight" => new QuietLight(),
        "QuietDark" => new QuietDark(),
        "GalleryLight" => new GalleryLight(),
        "GalleryDark" => new GalleryDark(),
        "NotebookLight" => new NotebookLight(),
        "NotebookDark" => new NotebookDark(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown theme dictionary."),
    };
}
