using CommunityToolkit.Mvvm.ComponentModel;
using ImageGen.Core.Domain.Models;
using ImageGen.Maui.Resources.Strings;

namespace ImageGen.Maui.PageModels;

public sealed partial class ModelOptionViewModel(ImageModel model, bool isSelected) : ObservableObject
{
    public string Id { get; } = model.Id;

    public string Name { get; } = model.Name;

    public string DetailsText { get; } = model.Id + " · " + (model.SupportsSeed ? AppResources.ModelSupportsSeed : AppResources.ModelNoSeed);

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
