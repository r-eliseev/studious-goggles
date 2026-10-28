using System.Globalization;
using ImageGen.Core.Domain.Generations;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

/// <summary>One run row: the prompt followed by the cards of its generations.</summary>
public sealed class RunItemViewModel
{
    /// <param name="copy">Copies the prompt of the run (without the «Prompt:» label).</param>
    public RunItemViewModel(Run run, IReadOnlyList<GenerationItemViewModel> generations, CopyTextAction copy)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(generations);
        Id = run.Id;
        PromptText = string.Format(CultureInfo.CurrentCulture, AppResources.RunPromptFormat, run.Prompt);
        Generations = generations;
        Copy = copy ?? throw new ArgumentNullException(nameof(copy));
    }

    public Guid Id { get; }

    public string PromptText { get; }

    public IReadOnlyList<GenerationItemViewModel> Generations { get; }

    public CopyTextAction Copy { get; }
}
