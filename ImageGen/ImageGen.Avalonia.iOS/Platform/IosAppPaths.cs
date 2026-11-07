using ImageGen.Core.Platform;

namespace ImageGen.Avalonia.Platform;

/// <summary>Uses the app's private iOS container for application data.</summary>
public sealed class IosAppPaths : IAppPaths
{
    public IosAppPaths()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImageGen");
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }
}
