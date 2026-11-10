using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Providers;

public interface IEmbeddingModelProvider
{
    Task<IReadOnlyList<EmbeddingModel>> ListEmbeddingModelsAsync(CancellationToken cancellationToken);
}
