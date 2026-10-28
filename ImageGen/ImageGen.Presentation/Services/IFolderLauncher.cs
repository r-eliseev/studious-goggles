namespace ImageGen.Presentation.Services;

/// <summary>Opens a folder in the system file manager. Each shell implements it; phones do not support it.</summary>
public interface IFolderLauncher
{
    /// <summary>False where the platform cannot show a folder (for example, phones); the screen hides the action then.</summary>
    bool IsSupported { get; }

    Task OpenAsync(string path, CancellationToken cancellationToken);
}
