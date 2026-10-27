namespace ImageGen.Core.Platform;

public interface IAppPaths
{
    /// <summary>Absolute path of the folder for the database and image files.</summary>
    string DataDirectory { get; }
}
