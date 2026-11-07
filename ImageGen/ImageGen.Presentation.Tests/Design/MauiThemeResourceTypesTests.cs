using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

public sealed class MauiThemeResourceTypesTests
{
    [Fact]
    public void DynamicResourceTypes_MatchMauiPropertyTypesAcrossPagesControlsAndThemes()
    {
        var root = MauiRoot();
        var themes = Directory.GetFiles(Path.Combine(root, "Resources", "Themes"), "*.xaml")
            .Where(file => !Path.GetFileName(file).Equals("Common.xaml", StringComparison.Ordinal))
            .Select(XDocument.Load)
            .ToArray();
        var markup = new[] { "Pages", "Controls" }
            .SelectMany(folder => Directory.GetFiles(Path.Combine(root, folder), "*.xaml"))
            .Select(XDocument.Load);

        foreach (var document in markup)
        {
            foreach (var element in document.Descendants())
            {
                AssertResourceType(element.Attribute("StrokeThickness")?.Value, "Double", themes);
                AssertResourceType(element.Attribute("TextColor")?.Value, "Color", themes);
                if (element.Name.LocalName == "Setter")
                {
                    var property = element.Attribute("Property")?.Value;
                    var expectedType = property switch
                    {
                        "StrokeThickness" => "Double",
                        "TextColor" => "Color",
                        _ => null,
                    };
                    if (expectedType is not null)
                    {
                        AssertResourceType(element.Attribute("Value")?.Value, expectedType, themes);
                    }
                }
            }
        }
    }

    private static void AssertResourceType(
        string? value,
        string expectedType,
        IReadOnlyList<XDocument> themes)
    {
        const string prefix = "{DynamicResource ";
        if (value is null || !value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith('}')) return;

        var key = value[prefix.Length..^1];
        foreach (var theme in themes)
        {
            var resource = theme.Descendants().SingleOrDefault(node =>
                node.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == key));
            Assert.True(resource is not null, $"Resource {key} was not found in theme {theme.BaseUri}.");
            Assert.Equal(expectedType, resource!.Name.LocalName);
        }
    }

    private static string MauiRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !Directory.Exists(Path.Combine(directory, "ImageGen", "ImageGen.Maui")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return Path.Combine(directory ?? throw new DirectoryNotFoundException("Could not locate the repository root."), "ImageGen", "ImageGen.Maui");
    }
}
