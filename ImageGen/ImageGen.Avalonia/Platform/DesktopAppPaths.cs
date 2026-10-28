using ImageGen.Core.Platform;

namespace ImageGen.Avalonia.Platform;

/// <summary>
/// Data folder of the desktop app: <c>ImageGen</c> under the user's local application data (ADR 0006). The environment
/// variable <c>IMAGEGEN_DATA_DIR</c> replaces the folder, for manual checks (a second profile) and for tests.
/// </summary>
public sealed class DesktopAppPaths : IAppPaths
{
    public const string DataDirectoryVariable = "IMAGEGEN_DATA_DIR";

    public DesktopAppPaths()
        : this(Environment.GetEnvironmentVariable(DataDirectoryVariable))
    {
    }

    /// <param name="overrideDirectory">The folder to use; null or blank means the default folder.</param>
    public DesktopAppPaths(string? overrideDirectory)
    {
        DataDirectory = string.IsNullOrWhiteSpace(overrideDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageGen")
            : Path.GetFullPath(overrideDirectory);
    }

    public string DataDirectory { get; }
}
