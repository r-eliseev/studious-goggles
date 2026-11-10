using ImageGen.Core.Platform;
using Microsoft.Data.Sqlite;

namespace ImageGen.Acceptance.Tests.Support;

/// <summary>A data folder unique to an acceptance scenario.</summary>
public sealed class TempAppPaths : IAppPaths, IDisposable
{
    public TempAppPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "imagegen-acceptance-" + Guid.NewGuid().ToString("N"));
    }

    public string DataDirectory { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }
}
