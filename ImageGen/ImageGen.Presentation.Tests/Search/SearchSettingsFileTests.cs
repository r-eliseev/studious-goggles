using ImageGen.Core.Platform;
using ImageGen.Core.Search;
using ImageGen.Presentation.Search;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImageGen.Presentation.Tests.Search;

public class SearchSettingsFileTests
{
    [Fact]
    public void Parse_Null_DisablesSemanticSearchWithoutWarning()
    {
        var (enabled, _, warning) = SearchSettingsFile.Parse(null);

        Assert.False(enabled);
        Assert.Null(warning);
    }

    [Fact]
    public void Parse_True_EnablesSemanticSearch()
    {
        var (enabled, _, warning) = SearchSettingsFile.Parse("{\"enabled\":true,\"model\":\"baai/bge-m3\"}");

        Assert.True(enabled);
        Assert.Null(warning);
    }

    [Fact]
    public void Parse_MissingModel_UsesDefaultAndWarns()
    {
        var parsed = SearchSettingsFile.Parse("{\"enabled\":true}");

        Assert.Equal(SemanticSearchModels.Default, parsed.Model);
        Assert.NotNull(parsed.Warning);
    }

    [Fact]
    public void Parse_InvalidModelValue_UsesDefaultAndWarns()
    {
        var parsed = SearchSettingsFile.Parse("{\"enabled\":true,\"model\":42}");

        Assert.Equal(SemanticSearchModels.Default, parsed.Model);
        Assert.NotNull(parsed.Warning);
    }

    [Fact]
    public void Parse_InvalidJson_DisablesSemanticSearchAndWarns()
    {
        var (enabled, _, warning) = SearchSettingsFile.Parse("{invalid");

        Assert.False(enabled);
        Assert.Contains("not valid JSON", warning);
    }

    [Fact]
    public void Parse_UnknownField_DisablesSemanticSearchAndWarns()
    {
        var (enabled, _, warning) = SearchSettingsFile.Parse("{\"enabled\":true,\"mode\":\"local\"}");

        Assert.False(enabled);
        Assert.Contains("unknown field", warning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serialize_ThenParse_RoundTripsEnabled(bool enabled)
    {
        var (actual, _, warning) = SearchSettingsFile.Parse(SearchSettingsFile.Serialize(enabled, SemanticSearchModels.Default));

        Assert.Equal(enabled, actual);
        Assert.Null(warning);
    }

    [Fact]
    public async Task SetModelAsync_RoundTripsSelectedModel()
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-search-settings", Guid.NewGuid().ToString("N"));
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(directory);
        var settings = new SearchSettingsFile(paths, NullLogger<SearchSettingsFile>.Instance);

        await settings.SetModelAsync("vendor/embedding-v2", TestContext.Current.CancellationToken);
        var reloaded = new SearchSettingsFile(paths, NullLogger<SearchSettingsFile>.Instance);

        Assert.Equal("vendor/embedding-v2", await reloaded.GetModelAsync(TestContext.Current.CancellationToken));
        Directory.Delete(directory, recursive: true);
    }
}
