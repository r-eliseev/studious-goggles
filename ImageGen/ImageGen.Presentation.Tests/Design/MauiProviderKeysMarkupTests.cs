using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

public sealed class MauiProviderKeysMarkupTests
{
    [Fact]
    public void ApiKeyPage_ForwardsProviderRouteAndUsesProviderText()
    {
        var root = MauiRoot();
        var page = XDocument.Load(Path.Combine(root, "Pages", "ApiKeyPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ApiKeyPage.xaml.cs"));

        Assert.Equal("{Binding ProviderTitle}", (string?)page.Root!.Attribute("Title"));
        Assert.Contains(page.Descendants().Where(element => element.Name.LocalName == "Label"),
            label => (string?)label.Attribute("Text") == "{Binding ProviderTitle}");
        Assert.Contains(page.Descendants().Where(element => element.Name.LocalName == "Label"),
            label => (string?)label.Attribute("Text") == "{Binding ProviderDescription}");
        Assert.Contains("IQueryAttributable", code, StringComparison.Ordinal);
        Assert.Contains("_model.ApplyQueryAttributes(query)", code, StringComparison.Ordinal);
        Assert.Contains("_model.OnAppearingAsync()", code, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPage_ShowsProviderKeyRowsWithChangeAndRemoveActions()
    {
        var page = XDocument.Load(Path.Combine(MauiRoot(), "Pages", "SettingsPage.xaml"));
        var layout = Assert.Single(page.Descendants(), element => element.Name.LocalName == "VerticalStackLayout"
            && (string?)element.Attribute("BindableLayout.ItemsSource") == "{Binding ProviderKeys}");
        var template = Assert.Single(layout.Descendants(), element => element.Name.LocalName == "DataTemplate");

        Assert.Equal("pm:ProviderKeyItemViewModel", (string?)template.Attributes().Single(attribute => attribute.Name.LocalName == "DataType"));
        Assert.Contains(template.Descendants(), element => (string?)element.Attribute("Command") == "{Binding ChangeCommand}");
        Assert.Contains(template.Descendants(), element => (string?)element.Attribute("Command") == "{Binding RemoveCommand}");
    }

    [Fact]
    public void ModelPickerAndSessionModels_ShowProviderLabels()
    {
        var root = MauiRoot();
        var picker = XDocument.Load(Path.Combine(root, "Pages", "ModelPickerPage.xaml"));
        var models = XDocument.Load(Path.Combine(root, "Controls", "SessionModelsView.xaml"));

        Assert.Contains(picker.Descendants(), element => element.Name.LocalName == "Label"
            && (string?)element.Attribute("Text") == "{Binding ProviderText}");
        Assert.Contains(models.Descendants(), element => element.Name.LocalName == "Label"
            && (string?)element.Attribute("Text") == "{Binding ProviderText}");
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
