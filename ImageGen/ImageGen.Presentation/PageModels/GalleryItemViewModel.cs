using CommunityToolkit.Mvvm.ComponentModel;
using ImageGen.Core.Domain.Gallery;

namespace ImageGen.Presentation.PageModels;

public sealed partial class GalleryItemViewModel(GalleryItem item, string imageFilePath) : ObservableObject
{
    public Guid GenerationId => item.GenerationId;
    public Guid SessionId => item.SessionId;
    public string RelativeImagePath => item.ImagePath;
    public string SessionTitle => item.SessionTitle;
    public string ModelText => $"{item.Model.ProviderId} · {item.Model.ModelId}";
    public string Prompt => item.Prompt;
    public string ImagePath => imageFilePath;
    public string CostText => item.CostUsd?.ToString("C", System.Globalization.CultureInfo.CurrentCulture) ?? "";

    [ObservableProperty]
    public partial bool IsUnavailable { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";
}
