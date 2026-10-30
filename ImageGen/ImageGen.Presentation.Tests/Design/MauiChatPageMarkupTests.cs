using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

public class MauiChatPageMarkupTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";

    [Fact]
    public void Transcript_IsNamedCollectionView()
    {
        var document = XDocument.Load(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml"));

        var transcript = Assert.Single(
            Assert.IsType<XElement>(document.Root).Descendants(),
            element => element.Name.LocalName == "CollectionView" && (string?)element.Attribute(Xaml + "Name") == "Transcript");

        Assert.Equal("{Binding Items}", (string?)transcript.Attribute("ItemsSource"));
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
