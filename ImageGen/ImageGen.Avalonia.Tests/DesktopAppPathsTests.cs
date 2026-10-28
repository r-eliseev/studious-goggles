using ImageGen.Avalonia.Platform;

namespace ImageGen.Avalonia.Tests;

public class DesktopAppPathsTests
{
    [Fact]
    public void Constructor_OverrideFolder_UsesIt()
    {
        var folder = Path.Combine(Path.GetTempPath(), "imagegen-paths-" + Guid.NewGuid().ToString("N"));

        var paths = new DesktopAppPaths(folder);

        Assert.Equal(Path.GetFullPath(folder), paths.DataDirectory);
    }

    [Fact]
    public void Constructor_BlankOverride_UsesDefaultFolder()
    {
        var paths = new DesktopAppPaths("  ");

        Assert.EndsWith(Path.Combine("ImageGen"), paths.DataDirectory, StringComparison.Ordinal);
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), paths.DataDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_EnvironmentVariable_SetsDataFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "imagegen-env-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable(DesktopAppPaths.DataDirectoryVariable);
        try
        {
            Environment.SetEnvironmentVariable(DesktopAppPaths.DataDirectoryVariable, folder);

            Assert.Equal(Path.GetFullPath(folder), new DesktopAppPaths().DataDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DesktopAppPaths.DataDirectoryVariable, previous);
        }
    }
}
