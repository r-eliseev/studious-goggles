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
        HeaderText = HeaderOf(generations);
        Copy = copy ?? throw new ArgumentNullException(nameof(copy));
    }

    public Guid Id { get; }

    public string PromptText { get; }

    /// <summary>Summary line of the run: model count, runs per model, done count and total cost.</summary>
    public string HeaderText { get; }

    private static string HeaderOf(IReadOnlyList<GenerationItemViewModel> generations)
    {
        var total = generations.Count;
        var models = generations.Select(g => g.ModelText).Distinct(StringComparer.Ordinal).Count();
        var runsPerModel = models > 0 ? total / models : 0;
        var done = generations.Count(g => g.StatusKind == GenerationStatusKind.Done);
        var cost = generations.Sum(g => g.CostUsd ?? 0m);
        var price = cost.ToString("0.####", CultureInfo.InvariantCulture);
        return string.Format(CultureInfo.CurrentCulture, AppResources.RunHeaderFormat, models, runsPerModel, done, total, price);
    }

    public IReadOnlyList<GenerationItemViewModel> Generations { get; }

    public CopyTextAction Copy { get; }
}
