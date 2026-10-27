using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Services;

public sealed class GenerationUpdatedEventArgs(Generation generation) : EventArgs
{
    /// <summary>Copy of the generation taken after it was saved.</summary>
    public Generation Generation { get; } = generation;
}
