using System.Text.RegularExpressions;

namespace ImageGen.Presentation.Tests.Design;

/// <summary>Checks the MAUI style files as text: the template palette is gone and the styles read only Ig* resources.</summary>
public class MauiStylesTests
{
    private static readonly Regex TemplateKey = new(
        @"\b(Primary|PrimaryDark|PrimaryDarkText|Secondary|Tertiary|Gray\d{2,3}|OffBlack|White|Black|OpenSans\w*|ErrorLight|ErrorDark)\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex ResourceReference = new(
        @"(?:Dynamic|Static)Resource\s+([A-Za-z0-9_]+)", RegexOptions.CultureInvariant);

    [Fact]
    public void Styles_DoNotReferenceTemplateKeys()
    {
        foreach (var file in MauiFiles())
        {
            Assert.DoesNotMatch(TemplateKey, File.ReadAllText(file));
        }
    }

    [Fact]
    public void Styles_UseOnlyIgResources()
    {
        var text = File.ReadAllText(Path.Combine(MauiRoot(), "Resources", "Styles", "Styles.xaml"));

        foreach (Match match in ResourceReference.Matches(text))
        {
            Assert.StartsWith("Ig", match.Groups[1].Value);
        }
    }

    [Fact]
    public void TemplateColorsFileIsRemoved()
    {
        Assert.False(File.Exists(Path.Combine(MauiRoot(), "Resources", "Styles", "Colors.xaml")));
    }

    private static IEnumerable<string> MauiFiles()
    {
        var root = MauiRoot();
        yield return Path.Combine(root, "Resources", "Styles", "Styles.xaml");
        foreach (var page in Directory.GetFiles(Path.Combine(root, "Pages"), "*.xaml"))
        {
            yield return page;
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
