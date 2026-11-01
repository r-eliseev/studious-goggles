using ImageGen.Core.Platform;

namespace ImageGen.Web;

public sealed class WebAppPaths : IAppPaths
{
    public const string DataDirectoryEnvironmentVariable = "IMAGEGEN_DATA_DIR";

    public WebAppPaths()
    {
        var configured = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        DataDirectory = string.IsNullOrWhiteSpace(configured)
            ? "/data"
            : Path.GetFullPath(configured);
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }
}
