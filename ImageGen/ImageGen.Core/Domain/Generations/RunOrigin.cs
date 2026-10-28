namespace ImageGen.Core.Domain.Generations;

/// <summary>What started a run.</summary>
public enum RunOrigin
{
    Assistant = 0,
    Repeat = 1,
    NewSeed = 2,
    Edit = 3,

    /// <summary>Run of one generation on a model the user picked (task 076): same prompt and seed, a different model.</summary>
    OtherModel = 4,
}
