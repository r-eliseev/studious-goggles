using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Storage;

public interface IEmbeddingModelRepository
{
    Task ReplaceAsync(string providerId, IReadOnlyList<EmbeddingModel> models, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmbeddingModel>> ListAsync(string providerId, CancellationToken cancellationToken);
}
