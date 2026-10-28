namespace ImageGen.Presentation.Services;

/// <summary>
/// Tells whether a saved secret (the API key) survives closing the app: the system secure storage is in use.
/// Asynchronous, because checking a platform store can call the system.
/// </summary>
public interface ISecretPersistence
{
    Task<bool> IsPersistentAsync(CancellationToken cancellationToken);
}
