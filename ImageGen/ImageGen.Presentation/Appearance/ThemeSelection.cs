namespace ImageGen.Presentation.Appearance;

/// <summary>Chooses the light or dark variant for the settings. Pure: the shells pass the system theme in.</summary>
public static class ThemeSelection
{
    public static bool IsDark(AppearanceTheme theme, bool systemIsDark) => theme switch
    {
        AppearanceTheme.Dark => true,
        AppearanceTheme.Light => false,
        _ => systemIsDark,
    };

    /// <summary>Name of the MAUI resource dictionary class for the style and variant, for example <c>GalleryDark</c>.</summary>
    public static string DictionaryName(AppearanceStyle style, bool isDark) =>
        style + (isDark ? "Dark" : "Light");
}
