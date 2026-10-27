namespace ImageGen.Core.Platform;

/// <summary>Encrypted platform storage for secrets such as API keys.</summary>
public interface ISecretStore
{
    Task<string?> GetAsync(string name, CancellationToken cancellationToken);

    Task SetAsync(string name, string value, CancellationToken cancellationToken);

    Task RemoveAsync(string name, CancellationToken cancellationToken);
}
