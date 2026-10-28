using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

/// <summary>
/// Reads the MAUI theme dictionaries as XML (there is no MAUI test project on Linux) and compares them with
/// docs/design/tokens.json.
/// </summary>
public class MauiThemeTokensTests
{
    private static readonly XNamespace Maui = "http://schemas.microsoft.com/dotnet/2021/maui";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";

    private static readonly string[] ColorNames =
        ["bg", "surface", "sunken", "fg", "muted", "line", "control", "accent", "onAccent", "accentSoft", "ok", "err", "errSoft", "warn", "warnSoft", "placeholder"];

    private static readonly string[] Dictionaries =
        ["QuietLight", "QuietDark", "GalleryLight", "GalleryDark", "NotebookLight", "NotebookDark"];

    [Fact]
    public void Dictionaries_ColorsMatchTokensJson()
    {
        var root = RepositoryRoot();
        var tokens = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "design", "tokens.json")))!["style"]!;

        foreach (var name in Dictionaries)
        {
            var style = name[..^(name.EndsWith("Light") ? 5 : 4)];
            var theme = name.EndsWith("Light") ? "light" : "dark";
            var token = style switch { "Quiet" => "a", "Gallery" => "b", _ => "c" };
            var colors = Colors(root, name);

            foreach (var color in ColorNames)
            {
                var hex = tokens[token]!["color"]![theme]![color]!["$value"]!["hex"]!.GetValue<string>();
                var key = $"Ig{char.ToUpperInvariant(color[0])}{color[1..]}Color";

                Assert.True(colors.TryGetValue(key, out var actual), $"{name}: missing {key}");
                Assert.Equal(hex, actual, ignoreCase: true);
            }
        }
    }

    [Fact]
    public void Dictionaries_HaveSameKeys()
    {
        var root = RepositoryRoot();
        var first = Keys(root, Dictionaries[0]);

        foreach (var name in Dictionaries.Skip(1))
        {
            Assert.Equal(first, Keys(root, name));
        }
    }

    [Fact]
    public void Common_HasPlatformTypeScale()
    {
        var keys = Keys(RepositoryRoot(), "Common", folder: "Themes");

        Assert.Contains("IgFontSizeCaption", keys);
        Assert.Contains("IgFontSizeDisplay", keys);
        Assert.Contains("IgControlHeight", keys);
    }

    private static Dictionary<string, string> Colors(string root, string name)
    {
        var doc = Load(root, name);
        return doc.Root!.Elements(Maui + "Color")
            .ToDictionary(e => e.Attribute(Xaml + "Key")!.Value, e => e.Value.Trim());
    }

    private static HashSet<string> Keys(string root, string name, string folder = "Themes")
    {
        var doc = Load(root, name, folder);
        return doc.Root!.Elements()
            .Select(e => e.Attribute(Xaml + "Key")?.Value)
            .Where(k => k is not null)
            .Select(k => k!)
            .ToHashSet();
    }

    private static XDocument Load(string root, string name, string folder = "Themes") =>
        XDocument.Load(Path.Combine(root, "ImageGen", "ImageGen.Maui", "Resources", folder, name + ".xaml"));

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "design", "tokens.json")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
