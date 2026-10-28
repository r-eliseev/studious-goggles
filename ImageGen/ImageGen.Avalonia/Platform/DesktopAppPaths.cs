using ImageGen.Core.Platform;

namespace ImageGen.Avalonia.Platform;

/// <summary>Data folder of the desktop app: <c>ImageGen</c> under the user's local application data (ADR 0006).</summary>
public sealed class DesktopAppPaths : IAppPaths
{
    public string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageGen");
}
