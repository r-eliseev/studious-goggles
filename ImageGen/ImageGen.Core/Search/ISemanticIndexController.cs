namespace ImageGen.Core.Search;

/// <summary>Controls opt-in indexing and exposes its current progress to the presentation layer.</summary>
public interface ISemanticIndexController : ISemanticIndexStatusProvider
{
    Task<int> GetSourceCountAsync(CancellationToken cancellationToken);

    Task EnableAsync(CancellationToken cancellationToken);

    Task DisableAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
