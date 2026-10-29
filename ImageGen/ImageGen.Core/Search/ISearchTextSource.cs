namespace ImageGen.Core.Search;

/// <summary>Gives every searchable text of every session. The implementation lives in the storage layer.</summary>
public interface ISearchTextSource
{
    Task<IReadOnlyList<SearchText>> ListAllAsync(CancellationToken cancellationToken);
}
