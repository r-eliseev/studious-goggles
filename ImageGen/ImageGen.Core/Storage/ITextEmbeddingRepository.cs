using ImageGen.Core.Domain.Expenses;
using ImageGen.Core.Search;

namespace ImageGen.Core.Storage;

public interface ITextEmbeddingRepository
{
    Task<IReadOnlyList<TextEmbedding>> ListAllAsync(CancellationToken cancellationToken);

    Task UpsertBatchAsync(IReadOnlyList<TextEmbedding> embeddings, Expense? expense, CancellationToken cancellationToken);

    Task DeleteBySourceKeysAsync(IReadOnlyCollection<string> sourceKeys, CancellationToken cancellationToken);

    Task DeleteAllAsync(CancellationToken cancellationToken);
}
