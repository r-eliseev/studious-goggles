using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

public class MauiSessionsListMarkupTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";

    [Fact]
    public void TodayAndEarlier_UseOneSharedRowTemplate()
    {
        var document = Load();
        var lists = document.Descendants()
            .Where(element => (string?)element.Attribute("BindableLayout.ItemsSource") is "{Binding Today}" or "{Binding Earlier}")
            .ToList();

        Assert.Equal(2, lists.Count);
        Assert.All(lists, list => Assert.Equal(
            "{OnIdiom Desktop={StaticResource SessionRowTemplateDesktop}, Default={StaticResource SessionRowTemplatePhone}}",
            (string?)list.Attribute("BindableLayout.ItemTemplate")));
    }

    [Fact]
    public void DesktopSessionRow_OpensByTapOnRowAndKeepsSwipeActions()
    {
        // On Android a Button inside the SwipeView lost the tap to the swipe, which showed Rename/Delete instead (task 162).
        var template = RowTemplate("SessionRowTemplateDesktop");
        var swipe = Assert.Single(template.Descendants(), element => element.Name.LocalName == "SwipeView");

        Assert.DoesNotContain(swipe.Descendants(), element => element.Name.LocalName == "Button"
            && (string?)element.Attribute("Command") == "{Binding OpenCommand}");
        Assert.Contains(swipe.Descendants(), element => element.Name.LocalName == "TapGestureRecognizer"
            && (string?)element.Attribute("Command") == "{Binding OpenCommand}");
        Assert.NotNull(swipe.Attribute("Threshold"));
    }

    [Fact]
    public void PhoneSessionRow_UsesTouchBehaviorForTapAndLongPressWithoutSwipe()
    {
        XNamespace toolkit = "http://schemas.microsoft.com/dotnet/2022/maui/toolkit";
        var template = RowTemplate("SessionRowTemplatePhone");
        var row = Assert.Single(template.Descendants(), element =>
            element.Name.LocalName == "Grid" && (string?)element.Attribute(Xaml + "Name") == "RowRoot");
        var behavior = Assert.Single(template.Descendants(toolkit + "TouchBehavior"));

        Assert.DoesNotContain(template.Descendants(), element => element.Name.LocalName == "SwipeView");
        Assert.Equal("{Binding OpenCommand}", (string?)behavior.Attribute("Command"));
        Assert.Equal("{Binding ShowActionsCommand}", (string?)behavior.Attribute("LongPressCommand"));
        Assert.Equal(
            "{Binding Source={x:Reference RowRoot}, Path=BindingContext, x:DataType=Grid}",
            (string?)behavior.Attribute("BindingContext"));
        Assert.Equal("{Binding Title}", (string?)row.Attribute("SemanticProperties.Description"));
        Assert.Equal("{x:Static strings:AppResources.SessionLongPressHint}", (string?)row.Attribute("SemanticProperties.Hint"));
    }

    [Fact]
    public void DesktopSessionRow_IsAccessibleAndKeepsSwipeActions()
    {
        var template = RowTemplate("SessionRowTemplateDesktop");
        var row = Assert.Single(template.Descendants(), element => element.Descendants().Any(child =>
            child.Name.LocalName == "TapGestureRecognizer") && element.Name.LocalName == "Grid");

        Assert.Equal("{Binding Title}", (string?)row.Attribute("SemanticProperties.Description"));
        Assert.Equal("{StaticResource IgControlHeight}", (string?)row.Attribute("MinimumHeightRequest"));
        Assert.Contains(template.Descendants(), element => element.Name.LocalName == "SwipeItem"
            && (string?)element.Attribute("Command") == "{Binding RenameCommand}");
        Assert.Contains(template.Descendants(), element => element.Name.LocalName == "SwipeItem"
            && (string?)element.Attribute("Command") == "{Binding DeleteCommand}");
    }

    private static XElement RowTemplate(string key) =>
        Assert.Single(Load().Descendants(), element => element.Name.LocalName == "DataTemplate"
            && (string?)element.Attribute(Xaml + "Key") == key);

    private static XDocument Load() => XDocument.Load(Path.Combine(MauiRoot(), "Controls", "SessionsListView.xaml"));

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
