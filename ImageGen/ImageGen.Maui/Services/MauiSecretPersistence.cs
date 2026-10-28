using ImageGen.Presentation.Services;

namespace ImageGen.Maui.Services;

/// <summary>SecureStorage (Keychain, Keystore, Windows data protection) keeps the key across restarts on every MAUI platform.</summary>
public sealed class MauiSecretPersistence : ISecretPersistence
{
    public Task<bool> IsPersistentAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}
