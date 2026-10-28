using ImageGen.Presentation.Appearance;

namespace ImageGen.Presentation.Tests.Appearance;

public class ThemeSelectionTests
{
    [Theory]
    [InlineData(AppearanceTheme.Dark, false, true)]
    [InlineData(AppearanceTheme.Dark, true, true)]
    [InlineData(AppearanceTheme.Light, false, false)]
    [InlineData(AppearanceTheme.Light, true, false)]
    [InlineData(AppearanceTheme.System, false, false)]
    [InlineData(AppearanceTheme.System, true, true)]
    public void IsDark_ExplicitThemeWinsSystemFollows(AppearanceTheme theme, bool systemIsDark, bool expected)
    {
        Assert.Equal(expected, ThemeSelection.IsDark(theme, systemIsDark));
    }

    [Theory]
    [InlineData(AppearanceStyle.Quiet, false, "QuietLight")]
    [InlineData(AppearanceStyle.Quiet, true, "QuietDark")]
    [InlineData(AppearanceStyle.Gallery, false, "GalleryLight")]
    [InlineData(AppearanceStyle.Gallery, true, "GalleryDark")]
    [InlineData(AppearanceStyle.Notebook, false, "NotebookLight")]
    [InlineData(AppearanceStyle.Notebook, true, "NotebookDark")]
    public void DictionaryName_IsStylePlusTheme(AppearanceStyle style, bool isDark, string expected)
    {
        Assert.Equal(expected, ThemeSelection.DictionaryName(style, isDark));
    }
}
