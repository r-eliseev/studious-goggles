using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Services;

/// <summary>
/// Chooses which images of the last run the assistant gets (S2.4). Pure: no I/O.
/// Only successful generations with a saved image count; the first <see cref="AssistantPrompts.MaxRunImages"/> by
/// <see cref="Generation.SortOrder"/> are taken.
/// </summary>
public static class RunImageSelector
{
    public static IReadOnlyList<Generation> Select(IEnumerable<Generation> generations)
    {
        ArgumentNullException.ThrowIfNull(generations);
        return generations
            .Where(g => g.Status == GenerationStatus.Succeeded && !string.IsNullOrEmpty(g.ImagePath))
            .OrderBy(g => g.SortOrder)
            .Take(AssistantPrompts.MaxRunImages)
            .ToList();
    }
}
