using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

public class MauiSafeAreaTests
{
    [Fact]
    public void Pages_RespectKeyboardSafeArea()
    {
        var pagesDirectory = Path.Combine(MauiRoot(), "Pages");
        foreach (var file in Directory.GetFiles(pagesDirectory, "*.xaml"))
        {
            var root = XDocument.Load(file).Root;

            Assert.True((string?)root?.Attribute("SafeAreaEdges") == "All", $"{Path.GetFileName(file)} must set SafeAreaEdges=\"All\".");
        }
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
