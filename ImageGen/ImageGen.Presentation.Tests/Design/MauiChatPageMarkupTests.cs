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

    [Fact]
    public void SuccessfulImage_HasAccessibleViewerCommand()
    {
        var document = XDocument.Load(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml"));
        var button = Assert.Single(Assert.IsType<XElement>(document.Root).Descendants(), element =>
            element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding Source={x:Reference Page}, Path=BindingContext.OpenImageCommand}");

        Assert.Equal("{Binding HasImage}", (string?)button.Attribute("IsVisible"));
        Assert.Equal("{Binding .}", (string?)button.Attribute("CommandParameter"));
        Assert.Equal("Fill", (string?)button.Attribute("HorizontalOptions"));
        Assert.Equal("Fill", (string?)button.Attribute("VerticalOptions"));
        Assert.Equal("Transparent", (string?)button.Attribute("BackgroundColor"));
    }

    [Fact]
    public void VideoPlayer_UsesLocalFileWithControlsAndNoAutoplay()
    {
        XNamespace toolkit = "http://schemas.microsoft.com/dotnet/2022/maui/toolkit";
        var document = XDocument.Load(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml"));
        var players = Assert.IsType<XElement>(document.Root).Descendants()
            .Where(element => element.Name == toolkit + "MediaElement")
            .ToList();
        Assert.Equal(2, players.Count);
        var player = Assert.Single(players, element => (string?)element.Attribute("HeightRequest") == "176");

        Assert.Equal("{Binding MediaFilePath, Converter={StaticResource FilePathToMediaSource}}", (string?)player.Attribute("Source"));
        Assert.Equal("False", (string?)player.Attribute("ShouldAutoPlay"));
        Assert.Equal("True", (string?)player.Attribute("ShouldShowPlaybackControls"));
        Assert.Equal("{Binding SummaryText}", (string?)player.Attribute("SemanticProperties.Description"));
        Assert.Contains(player.Descendants(), element =>
            element.Name.LocalName == "BindingCondition"
            && (string?)element.Attribute("Binding") == "{Binding HasMedia}"
            && (string?)element.Attribute("Value") == "True");
    }

    [Fact]
    public void ChatDisappearing_StopsMediaPlayback()
    {
        var code = File.ReadAllText(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml.cs"));

        Assert.Contains("protected override void OnDisappearing()", code, StringComparison.Ordinal);
        Assert.Contains("foreach (var player in _mediaPlayers)", code, StringComparison.Ordinal);
        Assert.Contains("player.Stop();", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TranscriptScrolled_UserGesture_DoesNotStartProgrammaticScroll()
    {
        // A run taller than the transcript is always the visible last row: scrolling to it on every gesture threw the
        // user back to the top of the run on Android (task 159).
        // A Windows checkout has CRLF line endings (text=auto), so the source is normalized before searching.
        var code = File.ReadAllText(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml.cs")).ReplaceLineEndings("\n");
        var start = code.IndexOf("private void OnTranscriptScrolled(", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = code.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        Assert.True(end > start, "The end of OnTranscriptScrolled was not found.");
        var body = code[start..end];

        Assert.DoesNotContain("ScrollToEndLater", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ScrollToEnd_OnAndroid_ScrollsToBottomOfContent()
    {
        var code = File.ReadAllText(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml.cs"));
        var scroller = File.ReadAllText(Path.Combine(MauiRoot(), "Platforms", "Android", "TranscriptScroller.cs"));

        Assert.Contains("TranscriptScroller.ScrollToBottom(", code, StringComparison.Ordinal);
        Assert.Contains("CanScrollVertically(1)", scroller, StringComparison.Ordinal);
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
