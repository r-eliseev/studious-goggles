using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Models;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

/// <summary>
/// One row of the assistant model list: name, id, price and the flags that matter for the choice. Only one row is
/// selected; <see cref="Select"/> reports the row to the page model, which clears the others.
/// </summary>
public sealed partial class AssistantModelOptionViewModel(
    ModelRef reference,
    ChatModel model,
    bool isSelected,
    Action<AssistantModelOptionViewModel> select) : ObservableObject
{
    /// <summary>Provider and model id: the value saved as the assistant model of the session.</summary>
    public ModelRef Reference { get; } = reference;

    public string Name { get; } = model.Name;

    /// <summary>Model id, price and whether it takes images, for example "vendor/model · price · takes images".</summary>
    public string DetailsText { get; } = BuildDetails(model);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(AutomationName))]
    public partial bool IsSelected { get; set; } = isSelected;

    /// <summary>Short labels of the row: free, takes images. Shown as tags next to the name.</summary>
    public IReadOnlyList<string> Tags { get; } = BuildTags(model);

    /// <summary>The name with a check mark for the selected row, shown on screen.</summary>
    public string DisplayName => IsSelected ? "✓ " + Name : Name;

    /// <summary>The name with the selected state, read by screen readers.</summary>
    public string AutomationName => IsSelected ? $"{Name}, {AppResources.AssistantSelected}" : Name;

    [RelayCommand]
    private void Select() => select(this);

    private static List<string> BuildTags(ChatModel model)
    {
        var tags = new List<string>();
        if (model.IsFree)
        {
            tags.Add(AppResources.AssistantFree);
        }

        if (model.SupportsImageInput)
        {
            tags.Add(AppResources.AssistantTakesImages);
        }

        return tags;
    }

    private static string BuildDetails(ChatModel model)
    {
        var parts = new List<string> { model.Id, PriceText(model) };
        if (model.SupportsImageInput)
        {
            parts.Add(AppResources.AssistantTakesImages);
        }

        return string.Join(" · ", parts);
    }

    private static string PriceText(ChatModel model)
    {
        if (model.IsFree)
        {
            return AppResources.AssistantFree;
        }

        if (model.PromptPricePerMillion is not { } prompt || model.CompletionPricePerMillion is not { } completion)
        {
            return AppResources.AssistantPriceUnknown;
        }

        // Prices use the invariant format, like the other prices on screen, until the UI is localized.
        return string.Format(
            CultureInfo.InvariantCulture,
            AppResources.AssistantPriceFormat,
            prompt.ToString("0.00##", CultureInfo.InvariantCulture),
            completion.ToString("0.00##", CultureInfo.InvariantCulture));
    }
}
