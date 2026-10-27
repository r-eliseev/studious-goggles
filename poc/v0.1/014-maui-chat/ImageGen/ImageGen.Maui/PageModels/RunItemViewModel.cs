using System.Collections.ObjectModel;
using System.Globalization;
using ImageGen.Core.Domain.Generations;
using ImageGen.Maui.Resources.Strings;

namespace ImageGen.Maui.PageModels;

public sealed class RunItemViewModel(Run run, IEnumerable<GenerationItemViewModel> generations)
{
    public Guid Id { get; } = run.Id;

    public string PromptText { get; } = string.Format(CultureInfo.CurrentCulture, AppResources.RunPromptFormat, run.Prompt);

    public ObservableCollection<GenerationItemViewModel> Generations { get; } = new(generations);
}
