using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;

namespace ImageGen.Avalonia.Tests.Appearance;

public class FontAssetsTests
{
    private static readonly string[] FontFiles =
    [
        "Onest-Regular.ttf",
        "Onest-Medium.ttf",
        "Onest-SemiBold.ttf",
        "IBMPlexSans-Regular.ttf",
        "IBMPlexSans-Medium.ttf",
        "IBMPlexSans-SemiBold.ttf",
        "IBMPlexMono-Regular.ttf",
        "IBMPlexMono-Medium.ttf",
        "lucide.ttf",
    ];

    [AvaloniaFact]
    public void Fonts_AreEmbeddedAsAssets()
    {
        foreach (var file in FontFiles)
        {
            var uri = new Uri($"avares://ImageGen.Avalonia/Assets/Fonts/{file}");

            Assert.True(AssetLoader.Exists(uri), $"missing font asset {file}");
        }
    }

    [AvaloniaFact]
    public void Licenses_AreEmbeddedAsAssets()
    {
        foreach (var file in new[] { "Onest-OFL.txt", "IBMPlexSans-OFL.txt", "IBMPlexMono-OFL.txt", "Lucide-LICENSE.txt" })
        {
            Assert.True(AssetLoader.Exists(new Uri($"avares://ImageGen.Avalonia/Assets/Licenses/{file}")), $"missing license {file}");
        }
    }
}
