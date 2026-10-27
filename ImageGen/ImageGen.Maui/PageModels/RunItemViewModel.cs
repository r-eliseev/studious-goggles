using System.Globalization;
using ImageGen.Core.Domain.Generations;
using ImageGen.Maui.Resources.Strings;

namespace ImageGen.Maui.PageModels;

/// <summary>One run row: the prompt followed by the cards of its generations.</summary>
public sealed class RunItemViewModel
{
    public RunItemViewModel(Run run, IReadOnlyList<GenerationItemViewModel> generations)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(generations);
        Id = run.Id;
        PromptText = string.Format(CultureInfo.CurrentCulture, AppResources.RunPromptFormat, run.Prompt);
        Generations = generations;
    }

    public Guid Id { get; }

    public string PromptText { get; }

    public IReadOnlyList<GenerationItemViewModel> Generations { get; }
}
