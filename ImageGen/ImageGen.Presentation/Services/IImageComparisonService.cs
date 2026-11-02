using ImageGen.Presentation.PageModels;

namespace ImageGen.Presentation.Services;

public interface IImageComparisonService
{
    Task ShowAsync(ComparisonPageModel model, CancellationToken cancellationToken);
}
