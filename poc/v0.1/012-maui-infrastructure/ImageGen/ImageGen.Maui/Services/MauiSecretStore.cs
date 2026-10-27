using ImageGen.Core.Platform;

namespace ImageGen.Maui.Services;

/// <summary>Secrets in MAUI SecureStorage (Keychain, Keystore, Windows data protection).</summary>
public sealed class MauiSecretStore : ISecretStore
{
    public Task<string?> GetAsync(string name, CancellationToken cancellationToken) =>
        SecureStorage.Default.GetAsync(name);

    public Task SetAsync(string name, string value, CancellationToken cancellationToken) =>
        SecureStorage.Default.SetAsync(name, value);

    public Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        SecureStorage.Default.Remove(name);
        return Task.CompletedTask;
    }
}
