using ImageGen.Presentation.Search;

namespace ImageGen.Presentation.Tests.Search;

public class SearchSettingsFileTests
{
    [Fact]
    public void Parse_Null_DisablesSemanticSearchWithoutWarning()
    {
        var (enabled, warning) = SearchSettingsFile.Parse(null);

        Assert.False(enabled);
        Assert.Null(warning);
    }

    [Fact]
    public void Parse_True_EnablesSemanticSearch()
    {
        var (enabled, warning) = SearchSettingsFile.Parse("{\"enabled\":true}");

        Assert.True(enabled);
        Assert.Null(warning);
    }

    [Fact]
    public void Parse_InvalidJson_DisablesSemanticSearchAndWarns()
    {
        var (enabled, warning) = SearchSettingsFile.Parse("{invalid");

        Assert.False(enabled);
        Assert.Contains("not valid JSON", warning);
    }

    [Fact]
    public void Parse_UnknownField_DisablesSemanticSearchAndWarns()
    {
        var (enabled, warning) = SearchSettingsFile.Parse("{\"enabled\":true,\"mode\":\"local\"}");

        Assert.False(enabled);
        Assert.Contains("unknown field", warning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serialize_ThenParse_RoundTripsEnabled(bool enabled)
    {
        var (actual, warning) = SearchSettingsFile.Parse(SearchSettingsFile.Serialize(enabled));

        Assert.Equal(enabled, actual);
        Assert.Null(warning);
    }
}
