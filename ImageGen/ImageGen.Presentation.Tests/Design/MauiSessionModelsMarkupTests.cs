using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

public class MauiSessionModelsMarkupTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";

    [Fact]
    public void SessionModelsView_Exists()
    {
        Assert.True(File.Exists(Path.Combine(MauiRoot(), "Controls", "SessionModelsView.xaml")));
    }

    [Fact]
    public void SessionModelsPage_UsesSharedView()
    {
        var text = File.ReadAllText(Path.Combine(MauiRoot(), "Pages", "SessionModelsPage.xaml"));

        Assert.Contains("controls:SessionModelsView", text);
        Assert.DoesNotContain("{Binding AssistantModels}", text);
    }

    [Fact]
    public void ChatPage_UsesSharedView()
    {
        var text = File.ReadAllText(Path.Combine(MauiRoot(), "Pages", "ChatPage.xaml"));

        Assert.Contains("controls:SessionModelsView", text);
        Assert.DoesNotContain("{Binding AssistantModels}", text);
    }

    [Fact]
    public void SessionModelsView_SaveAndCancel_AreOutsideScrollAndFixedRow()
    {
        var document = XDocument.Load(Path.Combine(MauiRoot(), "Controls", "SessionModelsView.xaml"));
        var root = Assert.IsType<XElement>(document.Root);
        var rootGrid = root.Elements().Single(element => element.Name.LocalName == "Grid");
        var rows = (string?)rootGrid.Attribute("RowDefinitions");
        var buttonRow = rootGrid.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == "ButtonRow");
        var save = rootGrid.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == "SaveButton");
        var cancel = rootGrid.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == "CancelButton");

        Assert.Equal("Auto,Auto,Auto,*,Auto", rows);
        Assert.Same(rootGrid, buttonRow.Parent);
        Assert.Equal("4", (string?)buttonRow.Attribute("Grid.Row"));
        Assert.DoesNotContain(buttonRow.Ancestors(), element => element.Name.LocalName == "ScrollView");
        Assert.DoesNotContain(save.Ancestors(), element => element.Name.LocalName == "ScrollView");
        Assert.DoesNotContain(cancel.Ancestors(), element => element.Name.LocalName == "ScrollView");
    }

    [Fact]
    public void SessionModelsView_TabsHaveAccessibleNamesAndCommands()
    {
        var document = XDocument.Load(Path.Combine(MauiRoot(), "Controls", "SessionModelsView.xaml"));
        var buttons = Assert.IsType<XElement>(document.Root).Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .ToList();
        var imageTab = buttons.Single(element => (string?)element.Attribute("Command") == "{Binding ShowImageModelsCommand}");
        var assistantTab = buttons.Single(element => (string?)element.Attribute("Command") == "{Binding ShowAssistantModelCommand}");
        var groupToggles = buttons.Where(element => (string?)element.Attribute("Command") == "{Binding ToggleCommand}").ToArray();

        Assert.Equal("{x:Static strings:AppResources.ImageModelsTab}", (string?)imageTab.Attribute("Text"));
        Assert.Equal("{Binding ImageModelsTabAutomationName}", (string?)imageTab.Attribute("SemanticProperties.Description"));
        Assert.Equal("{x:Static strings:AppResources.AssistantModelTitle}", (string?)assistantTab.Attribute("Text"));
        Assert.Equal("{Binding AssistantTabAutomationName}", (string?)assistantTab.Attribute("SemanticProperties.Description"));
        Assert.Equal(2, groupToggles.Length);
        Assert.All(groupToggles, button => Assert.Equal("{Binding AutomationName}", (string?)button.Attribute("SemanticProperties.Description")));
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
