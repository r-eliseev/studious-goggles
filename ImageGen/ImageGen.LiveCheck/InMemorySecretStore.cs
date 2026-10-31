using System.Collections.Concurrent;
using ImageGen.Core.Platform;

namespace ImageGen.LiveCheck;

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> values = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(values.GetValueOrDefault(name));

    public Task SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        values[name] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string name, CancellationToken cancellationToken)
    {
        values.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
