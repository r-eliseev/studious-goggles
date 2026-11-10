namespace ImageGen.Core.Search;

public interface ISemanticSearchSettingsStore
{
    Task<bool> GetEnabledAsync(CancellationToken cancellationToken);

    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken);

    Task<string> GetModelAsync(CancellationToken cancellationToken);

    Task SetModelAsync(string modelId, CancellationToken cancellationToken);
}
