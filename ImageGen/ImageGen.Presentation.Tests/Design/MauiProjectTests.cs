using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

public class MauiProjectTests
{
    [Fact]
    public void ApplicationId_AllMauiPlatforms_UseApprovedPackageName()
    {
        var project = XDocument.Load(Path.Combine(MauiRoot(), "ImageGen.Maui.csproj"));
        var ids = project.Descendants().Where(element => element.Name.LocalName == "ApplicationId").ToList();

        Assert.Single(ids);
        Assert.Equal("com.eliseev.imagegen", ids[0].Value);

        var title = project.Descendants().Single(element => element.Name.LocalName == "ApplicationTitle");
        Assert.Equal("Omnilite", title.Value);
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
