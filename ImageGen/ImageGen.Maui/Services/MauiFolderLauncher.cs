using ImageGen.Presentation.Services;
using Microsoft.Maui.ApplicationModel;

namespace ImageGen.Maui.Services;

/// <summary>Opens a folder in the file manager. Only desktop shells can show a folder; phones hide the action.</summary>
public sealed class MauiFolderLauncher : IFolderLauncher
{
    public bool IsSupported => DeviceInfo.Idiom == DeviceIdiom.Desktop;

    public Task OpenAsync(string path, CancellationToken cancellationToken) =>
        IsSupported ? Launcher.Default.OpenAsync(new Uri(path)) : Task.CompletedTask;
}
