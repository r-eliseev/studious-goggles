namespace ImageGen.Core.Domain.Generations;

/// <summary>What started a run.</summary>
public enum RunOrigin
{
    Assistant = 0,
    Repeat = 1,
    NewSeed = 2,
    Edit = 3,
}
