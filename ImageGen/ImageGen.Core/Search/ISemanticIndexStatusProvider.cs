namespace ImageGen.Core.Search;

public interface ISemanticIndexStatusProvider
{
    event EventHandler<SemanticIndexStatus>? StatusChanged;

    Task<SemanticIndexStatus> GetStatusAsync(CancellationToken cancellationToken);

    SemanticIndexStatus GetStatusSnapshot();
}
