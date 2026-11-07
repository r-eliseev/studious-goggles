using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Platform;

/// <summary>iOS does not expose a general folder-opening contract for this application.</summary>
public sealed class UnsupportedFolderLauncher : IFolderLauncher
{
    public bool IsSupported => false;

    public Task OpenAsync(string path, CancellationToken cancellationToken) => Task.CompletedTask;
}
