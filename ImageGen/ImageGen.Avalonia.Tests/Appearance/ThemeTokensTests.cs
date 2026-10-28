using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation.Appearance;

namespace ImageGen.Avalonia.Tests.Appearance;

public class ThemeTokensTests
{
    private static readonly string[] ColorNames =
        ["bg", "surface", "sunken", "fg", "muted", "line", "control", "accent", "onAccent", "accentSoft", "ok", "err", "errSoft", "warn", "warnSoft", "placeholder"];

    private static readonly (AppearanceStyle Style, string Token)[] Styles =
        [(AppearanceStyle.Quiet, "a"), (AppearanceStyle.Gallery, "b"), (AppearanceStyle.Notebook, "c")];

    [AvaloniaFact]
    public void Dictionaries_MatchTokensJson()
    {
        var tokens = ReadTokens();
        foreach (var (style, token) in Styles)
        {
            var dictionary = Load(style);
            foreach (var (theme, variant) in new[] { ("light", ThemeVariant.Light), ("dark", ThemeVariant.Dark) })
            {
                var themed = (ResourceDictionary)dictionary.ThemeDictionaries[variant];
                foreach (var name in ColorNames)
                {
                    var hex = tokens[token]!["color"]![theme]![name]!["$value"]!["hex"]!.GetValue<string>();
                    var key = $"Ig{char.ToUpperInvariant(name[0])}{name[1..]}";

                    Assert.Equal(Color.Parse(hex), (Color)themed[key + "Color"]!);
                    Assert.Equal(Color.Parse(hex), ((ISolidColorBrush)themed[key + "Brush"]!).Color);
                }
            }
        }
    }

    [AvaloniaFact]
    public void Dictionaries_HaveSameKeysInEveryStyle()
    {
        var keys = Styles.Select(s => KeysOf(Load(s.Style))).ToList();

        Assert.Equal(keys[0], keys[1]);
        Assert.Equal(keys[0], keys[2]);
    }

    [AvaloniaFact]
    public void ThemeUri_PointsToStyleDictionary()
    {
        Assert.Equal("avares://ImageGen.Avalonia/Themes/Gallery.axaml", AvaloniaAppearanceApplier.ThemeUri(AppearanceStyle.Gallery));
    }

    private static ResourceDictionary Load(AppearanceStyle style) =>
        (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri(AvaloniaAppearanceApplier.ThemeUri(style)));

    private static HashSet<string> KeysOf(ResourceDictionary dictionary)
    {
        var keys = dictionary.Keys.Select(k => k.ToString()!).ToHashSet();
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            foreach (var key in ((ResourceDictionary)dictionary.ThemeDictionaries[variant]).Keys)
            {
                keys.Add($"{variant}:{key}");
            }
        }

        return keys;
    }

    private static JsonNode ReadTokens()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "design", "tokens.json")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return JsonNode.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "docs", "design", "tokens.json")))!["style"]!;
    }
}
