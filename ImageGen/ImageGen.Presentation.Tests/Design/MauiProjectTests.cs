using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

public class MauiProjectTests
{
    [Fact]
    public void ApplicationId_OnAndroid_IsApprovedPackageNameAndOtherPlatformsKeepTheirs()
    {
        // Android moves to the approved id together with the release signing key (task 164); Windows keeps its id until
        // the data and secret migration of task 030.
        var project = XDocument.Load(Path.Combine(MauiRoot(), "ImageGen.Maui.csproj"));
        var ids = project.Descendants().Where(element => element.Name.LocalName == "ApplicationId").ToList();

        var android = Assert.Single(ids, id => ((string?)id.Attribute("Condition"))?.Contains("'android'", StringComparison.Ordinal) == true);
        Assert.Equal("com.eliseev.imagegen", android.Value);
        var common = Assert.Single(ids, id => id.Attribute("Condition") is null);
        Assert.Equal("com.companyname.imagegen.maui", common.Value);
        Assert.True(ids.IndexOf(common) < ids.IndexOf(android), "The Android id must come after the common one to override it.");
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
