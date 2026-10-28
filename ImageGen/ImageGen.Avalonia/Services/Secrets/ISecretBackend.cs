namespace ImageGen.Avalonia.Services.Secrets;

/// <summary>One system store for secrets (ADR 0008). Values are text; the backend encrypts or stores them itself.</summary>
public interface ISecretBackend
{
    /// <summary>True when the store can be used now (service running, collection present, platform supported).</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    Task<string?> GetAsync(string name, CancellationToken cancellationToken);

    Task SetAsync(string name, string value, CancellationToken cancellationToken);

    Task RemoveAsync(string name, CancellationToken cancellationToken);
}
