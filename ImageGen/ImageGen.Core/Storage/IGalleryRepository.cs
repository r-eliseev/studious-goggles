using ImageGen.Core.Domain.Gallery;
using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Storage;

public interface IGalleryRepository
{
    Task<GalleryPage> ListAsync(GalleryQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<ModelRef>> ListModelsAsync(CancellationToken cancellationToken);
}
