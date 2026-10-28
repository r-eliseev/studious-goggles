using CommunityToolkit.Mvvm.ComponentModel;
using ImageGen.Core.Domain.Models;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

/// <summary>One model row of the session models page: name, id with seed support, and the selection checkbox.</summary>
public sealed partial class ModelOptionViewModel(ImageModel model, bool isSelected) : ObservableObject
{
    public string Id { get; } = model.Id;

    public string Name { get; } = model.Name;

    /// <summary>Model id and whether it accepts a seed, for example "vendor/model · seed".</summary>
    public string DetailsText { get; } =
        model.Id + " · " + (model.SupportsSeed ? AppResources.ModelSupportsSeed : AppResources.ModelNoSeed);

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
