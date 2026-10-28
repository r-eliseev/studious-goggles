using System.Collections.Concurrent;
using ImageGen.Core.Platform;

namespace ImageGen.Avalonia.Platform;

/// <summary>
/// Secrets live only in memory of the running process (ADR 0006: used when no system store is available).
/// Task 040 replaces this with the system stores (DPAPI, Keychain, Secret Service).
/// </summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new();

    public Task<string?> GetAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(_secrets.TryGetValue(name, out var value) ? value : null);

    public Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        _secrets[name] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        _secrets.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
