using ImageGen.Core.Platform;

namespace ImageGen.Maui.Services;

/// <summary>Secrets in MAUI SecureStorage (Keychain, Keystore, Windows data protection).</summary>
public sealed class MauiSecretStore : ISecretStore
{
    /// <summary>Returns null when the platform store cannot be read (for example after a key loss): the key is then asked again.</summary>
    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task SetAsync(string name, string value, CancellationToken cancellationToken) =>
        SecureStorage.Default.SetAsync(name, value);

    public Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        SecureStorage.Default.Remove(name);
        return Task.CompletedTask;
    }
}
