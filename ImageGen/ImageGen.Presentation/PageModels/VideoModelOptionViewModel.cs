using CommunityToolkit.Mvvm.ComponentModel;
using ImageGen.Core.Domain.Models;

namespace ImageGen.Presentation.PageModels;

public sealed partial class VideoModelOptionViewModel(
    ModelRef reference,
    string name,
    string priceText,
    bool generatesAudio,
    bool isSelected) : ObservableObject
{
    public ModelRef Reference { get; } = reference;

    public string Name { get; } = name;

    public string PriceText { get; } = priceText;

    public bool GeneratesAudio { get; } = generatesAudio;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
