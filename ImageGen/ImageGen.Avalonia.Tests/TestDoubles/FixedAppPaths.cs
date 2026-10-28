using ImageGen.Core.Platform;

namespace ImageGen.Avalonia.Tests.TestDoubles;

/// <summary>Data folder for tests: nothing is created by the composition root, so any path works.</summary>
public sealed class FixedAppPaths : IAppPaths
{
    public string DataDirectory => Path.Combine(Path.GetTempPath(), "imagegen-avalonia-tests");
}
