using ImageGen.Core.Platform;

namespace ImageGen.Avalonia.Platform;

public sealed class AndroidAppPaths : IAppPaths
{
    public AndroidAppPaths()
    {
        DataDirectory = Path.Combine(global::Android.App.Application.Context.FilesDir!.AbsolutePath, "ImageGen");
        Directory.CreateDirectory(DataDirectory);
    }
    public string DataDirectory { get; }
}
