using ImageGen.Presentation.Appearance;

namespace ImageGen.Presentation.Tests.Appearance;

public class AppearanceSettingsFileTests
{
    [Fact]
    public void Parse_Null_ReturnsDefaultWithoutWarning()
    {
        var (settings, warning) = AppearanceSettingsFile.Parse(null);

        Assert.Equal(AppearanceSettings.Default, settings);
        Assert.Null(warning);
    }

    [Fact]
    public void Parse_GalleryDark_ReturnsGalleryDark()
    {
        var (settings, warning) = AppearanceSettingsFile.Parse("{\"style\":\"gallery\",\"theme\":\"dark\"}");

        Assert.Equal(new AppearanceSettings(AppearanceStyle.Gallery, AppearanceTheme.Dark), settings);
        Assert.Null(warning);
    }

    [Fact]
    public void Parse_UpperCaseValues_AreAccepted()
    {
        var (settings, warning) = AppearanceSettingsFile.Parse("{\"style\":\"NOTEBOOK\",\"theme\":\"Light\"}");

        Assert.Equal(new AppearanceSettings(AppearanceStyle.Notebook, AppearanceTheme.Light), settings);
        Assert.Null(warning);
    }

    [Fact]
    public void Parse_MissingTheme_UsesSystemWithoutWarning()
    {
        var (settings, warning) = AppearanceSettingsFile.Parse("{\"style\":\"gallery\"}");

        Assert.Equal(new AppearanceSettings(AppearanceStyle.Gallery, AppearanceTheme.System), settings);
        Assert.Null(warning);
    }

    [Fact]
    public void Parse_UnknownStyle_UsesQuietAndWarns()
    {
        var (settings, warning) = AppearanceSettingsFile.Parse("{\"style\":\"x\",\"theme\":\"dark\"}");

        Assert.Equal(new AppearanceSettings(AppearanceStyle.Quiet, AppearanceTheme.Dark), settings);
        Assert.Contains("unknown style", warning);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsDefaultAndWarns()
    {
        var (settings, warning) = AppearanceSettingsFile.Parse("{not json");

        Assert.Equal(AppearanceSettings.Default, settings);
        Assert.Contains("not valid JSON", warning);
    }

    [Fact]
    public void Parse_JsonArray_ReturnsDefaultAndWarns()
    {
        var (settings, warning) = AppearanceSettingsFile.Parse("[\"gallery\"]");

        Assert.Equal(AppearanceSettings.Default, settings);
        Assert.NotNull(warning);
    }

    [Fact]
    public void Serialize_ThenParse_RoundTripsEveryCombination()
    {
        foreach (var style in Enum.GetValues<AppearanceStyle>())
        {
            foreach (var theme in Enum.GetValues<AppearanceTheme>())
            {
                var original = new AppearanceSettings(style, theme);

                var (settings, warning) = AppearanceSettingsFile.Parse(AppearanceSettingsFile.Serialize(original));

                Assert.Equal(original, settings);
                Assert.Null(warning);
            }
        }
    }

    [Fact]
    public void Serialize_WritesLowerCaseValues()
    {
        var json = AppearanceSettingsFile.Serialize(new AppearanceSettings(AppearanceStyle.Gallery, AppearanceTheme.System));

        Assert.Equal("{\"style\":\"gallery\",\"theme\":\"system\"}", json);
    }
}
