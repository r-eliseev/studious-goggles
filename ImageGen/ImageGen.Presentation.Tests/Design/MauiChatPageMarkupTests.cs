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

    [Fact]
    public void ChatHeader_IsWrappingFlexLayout()
    {
        var document = XDocument.Load(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml"));

        Assert.Contains(Assert.IsType<XElement>(document.Root).Descendants(), element =>
            element.Name.LocalName == "FlexLayout"
            && (string?)element.Attribute(Xaml + "Name") == "ChatHeader"
            && (string?)element.Attribute("Wrap") == "Wrap"
            && (string?)element.Attribute("Direction") == "Row"
            && (string?)element.Attribute("AlignItems") == "Center"
            && element.Attribute("HorizontalOptions") is null);
        Assert.DoesNotContain(Assert.IsType<XElement>(document.Root).Descendants(), element =>
            element.Name.LocalName == "HorizontalStackLayout"
            && element.Elements().FirstOrDefault()?.Name.LocalName == "Label"
            && (string?)element.Elements().First().Attribute("Text") == "{Binding Title}");
    }

    [Fact]
    public void ChatTitle_HiddenOnNarrowAndTruncated()
    {
        var document = XDocument.Load(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml"));
        var title = Assert.Single(Assert.IsType<XElement>(document.Root).Descendants(), element =>
            element.Name.LocalName == "Label" && (string?)element.Attribute(Xaml + "Name") == "ChatTitle");

        Assert.Equal("{Binding Title}", (string?)title.Attribute("Text"));
        Assert.Equal("{Binding IsWide}", (string?)title.Attribute("IsVisible"));
        Assert.Equal("1", (string?)title.Attribute("MaxLines"));
        Assert.Equal("TailTruncation", (string?)title.Attribute("LineBreakMode"));
    }

    [Fact]
    public void AssistantChip_Truncated()
    {
        var document = XDocument.Load(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml"));
        var label = Assert.Single(Assert.IsType<XElement>(document.Root).Descendants(), element =>
            element.Name.LocalName == "Label" && (string?)element.Attribute(Xaml + "Name") == "AssistantChipText");

        Assert.Equal("{Binding AssistantText}", (string?)label.Attribute("Text"));
        Assert.Equal("1", (string?)label.Attribute("MaxLines"));
        Assert.Equal("TailTruncation", (string?)label.Attribute("LineBreakMode"));
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
