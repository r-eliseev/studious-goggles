using ImageGen.Core.Platform;

namespace ImageGen.Avalonia.Platform;

/// <summary>Uses Android's private files directory for application data.</summary>
public sealed class AndroidAppPaths : IAppPaths
{
    public AndroidAppPaths()
    {
        DataDirectory = Path.Combine(global::Android.App.Application.Context.FilesDir!.AbsolutePath, "ImageGen");
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }
}
