using ImageGen.Avalonia.Platform;
using ImageGen.Core.Platform;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services.Secrets;

/// <summary>
/// Secrets of the desktop app (ADR 0008): the system store of the platform when it is available, otherwise process memory.
/// The first availability check decides for the whole run, so a key never moves between stores while the app is open.
/// </summary>
public sealed class DesktopSecretStore(ISecretBackend? backend) : ISecretStore, ISecretPersistence
{
    private readonly InMemorySecretStore _memory = new();
    private readonly Lock _lock = new();
    private Task<bool>? _persistent;

    public static DesktopSecretStore ForCurrentPlatform(IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        ISecretBackend? backend = null;
        if (OperatingSystem.IsWindows())
        {
            backend = new DpapiSecretBackend(paths.DataDirectory);
        }
        else if (OperatingSystem.IsMacOS())
        {
            backend = new KeychainSecretBackend();
        }
        else if (OperatingSystem.IsLinux())
        {
            backend = new SecretServiceBackend();
        }

        return new DesktopSecretStore(backend);
    }

    public Task<bool> IsPersistentAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return _persistent ??= ProbeAsync();
        }
    }

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        if (!await IsPersistentAsync(cancellationToken))
        {
            return await _memory.GetAsync(name, cancellationToken);
        }

        try
        {
            return await backend!.GetAsync(name, cancellationToken);
        }
        catch (Exception)
        {
            // Same as MauiSecretStore: a store that cannot be read means the key is asked again.
            return null;
        }
    }

    public async Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        if (await IsPersistentAsync(cancellationToken))
        {
            await backend!.SetAsync(name, value, cancellationToken);
            return;
        }

        await _memory.SetAsync(name, value, cancellationToken);
    }

    public async Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        if (await IsPersistentAsync(cancellationToken))
        {
            await backend!.RemoveAsync(name, cancellationToken);
            return;
        }

        await _memory.RemoveAsync(name, cancellationToken);
    }

    private async Task<bool> ProbeAsync()
    {
        if (backend is null)
        {
            return false;
        }

        try
        {
            return await backend.IsAvailableAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
