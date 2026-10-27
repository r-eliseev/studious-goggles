using ImageGen.Core.Platform;

namespace ImageGen.Maui.Services;

public sealed class MauiAppPaths : IAppPaths
{
    public string DataDirectory => FileSystem.Current.AppDataDirectory;
}
