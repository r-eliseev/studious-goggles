namespace ImageGen.Core.Domain.Generations;

public enum ParameterOutcomeKind
{
    /// <summary>The requested value is sent as is.</summary>
    Applied,

    /// <summary>The requested value is not allowed; the nearest allowed value is sent.</summary>
    Replaced,

    /// <summary>Nothing is sent for this parameter.</summary>
    Dropped,
}
