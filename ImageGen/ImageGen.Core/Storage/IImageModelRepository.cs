using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Storage;

public interface IImageModelRepository
{
    /// <summary>Deletes the cached models of this provider and inserts the given ones in one transaction. Other providers' models stay.</summary>
    Task ReplaceAllAsync(string providerId, IReadOnlyList<ImageModel> models, CancellationToken cancellationToken);

    /// <summary>Cached models of the provider ordered by <see cref="ImageModel.Name"/> ascending.</summary>
    Task<IReadOnlyList<ImageModel>> ListAsync(string providerId, CancellationToken cancellationToken);
}
