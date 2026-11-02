namespace ImageGen.Core.Search;

public interface ISemanticSearchSettingsStore
{
    Task<bool> GetEnabledAsync(CancellationToken cancellationToken);

    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken);
}
