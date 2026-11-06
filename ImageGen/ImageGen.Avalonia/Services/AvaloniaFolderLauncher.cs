using Avalonia.Controls;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>Opens a folder in the system file manager through the launcher of the window that shows the app.</summary>
public sealed class AvaloniaFolderLauncher(Func<TopLevel?> owner) : IFolderLauncher
{
    public bool IsSupported => true;

    public async Task OpenAsync(string path, CancellationToken cancellationToken)
    {
        var launcher = owner()?.Launcher;
        if (launcher is not null)
        {
            await launcher.LaunchUriAsync(new Uri(path));
        }
    }
}
