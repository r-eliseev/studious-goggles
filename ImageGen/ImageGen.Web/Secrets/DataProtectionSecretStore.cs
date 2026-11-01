using System.Security.Cryptography;
using System.Text.Json;
using ImageGen.Core.Platform;
using Microsoft.AspNetCore.DataProtection;

namespace ImageGen.Web.Secrets;

public sealed class DataProtectionSecretStore(IAppPaths paths, IDataProtectionProvider protectionProvider) : ISecretStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("ImageGen.Secrets");
    private readonly string _filePath = Path.Combine(paths.DataDirectory, "secrets.json");

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var values = await ReadAsync(cancellationToken);
            if (!values.TryGetValue(name, out var protectedValue))
            {
                return null;
            }

            try
            {
                return _protector.Unprotect(protectedValue);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var values = await ReadAsync(cancellationToken);
            values[name] = _protector.Protect(value);
            await WriteAsync(values, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var values = await ReadAsync(cancellationToken);
            if (values.Remove(name))
            {
                await WriteAsync(values, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, string>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private async Task WriteAsync(Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        var temporaryPath = _filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, values, cancellationToken: cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
