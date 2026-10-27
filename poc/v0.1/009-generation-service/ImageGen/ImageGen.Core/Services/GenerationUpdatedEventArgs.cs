using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Services;

/// <summary>Carries a snapshot of the generation after it was saved.</summary>
public sealed class GenerationUpdatedEventArgs(Generation generation) : EventArgs
{
    public Generation Generation { get; } = generation;
}
