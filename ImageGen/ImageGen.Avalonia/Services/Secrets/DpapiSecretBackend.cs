using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace ImageGen.Avalonia.Services.Secrets;

/// <summary>
/// Windows: DPAPI (current user). Each secret is a file <c>secrets/&lt;name&gt;.bin</c> in the data folder; the file holds
/// only the encrypted bytes (ADR 0008).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretBackend(string dataDirectory) : ISecretBackend
{
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        Task.FromResult(OperatingSystem.IsWindows());

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var path = SecretFileNames.PathFor(dataDirectory, name);
        if (!File.Exists(path))
        {
            return null;
        }

        var encrypted = await File.ReadAllBytesAsync(path, cancellationToken);
        var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }

    public async Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        var path = SecretFileNames.PathFor(dataDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);

        var temporary = path + ".tmp";
        await File.WriteAllBytesAsync(temporary, encrypted, cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }

    public Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        var path = SecretFileNames.PathFor(dataDirectory, name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
