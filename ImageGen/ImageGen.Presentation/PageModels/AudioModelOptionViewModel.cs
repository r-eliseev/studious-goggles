using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageGen.Core.Domain.Models;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

public sealed partial class AudioModelOptionViewModel(AudioModel model, bool isSelected) : ObservableObject
{
    public ModelRef Reference { get; } = new(model.ProviderId, model.Id);

    public string Name { get; } = model.Name;

    public AudioOutput Output { get; } = model.Output;

    public string PriceText { get; } = model.Output == AudioOutput.Speech && model.PricePerInputCharacterUsd is { } price
        ? string.Format(CultureInfo.CurrentCulture, AppResources.AudioPricePerThousandFormat,
            (price * 1000m).ToString("$0.####", CultureInfo.InvariantCulture))
        : AppResources.AudioPriceUnknown;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
