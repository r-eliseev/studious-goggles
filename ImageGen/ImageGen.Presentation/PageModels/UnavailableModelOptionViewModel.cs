using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Models;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

/// <summary>A selected model reference absent from the latest non-empty provider catalog.</summary>
public sealed partial class UnavailableModelOptionViewModel(ModelRef reference, string providerText, Action? remove) : ObservableObject
{
    public ModelRef Reference { get; } = reference;

    public string Name { get; } = string.Format(AppResources.SelectedModelUnavailableFormat, reference.ModelId);

    public string ProviderText { get; } = providerText;

    public bool CanRemove { get; } = remove is not null;

    [RelayCommand]
    private void Remove() => remove?.Invoke();
}
