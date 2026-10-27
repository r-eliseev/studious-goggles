using ImageGen.Core.Domain;

namespace ImageGen.Core.Storage;

public interface IImageModelRepository
{
    /// <summary>Deletes all cached models and inserts the given ones in one transaction.</summary>
    Task ReplaceAllAsync(IReadOnlyList<ImageModel> models, CancellationToken cancellationToken);

    /// <summary>Cached models ordered by <see cref="ImageModel.Name"/> ascending.</summary>
    Task<IReadOnlyList<ImageModel>> ListAsync(CancellationToken cancellationToken);
}
