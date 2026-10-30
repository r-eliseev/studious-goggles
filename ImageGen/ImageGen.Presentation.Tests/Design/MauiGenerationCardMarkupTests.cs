using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

public sealed class MauiGenerationCardMarkupTests
{
    [Fact]
    public void ChatPage_ShowsImageDimensionsInStandaloneMetaLabel()
    {
        var document = XDocument.Load(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml"));
        var labels = Assert.IsType<XElement>(document.Root).Descendants()
            .Where(element => element.Name.LocalName == "Label"
                && (string?)element.Attribute("Text") == "{Binding ImageDimensionsText}")
            .ToList();

        var label = Assert.Single(labels);
        Assert.Equal("{Binding HasImageDimensions}", (string?)label.Attribute("IsVisible"));
    }

    private static string MauiRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "design", "tokens.json")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "ImageGen", "ImageGen.Maui");
    }
}
