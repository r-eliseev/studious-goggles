namespace ImageGen.Core.Domain.Generations;

public enum ParameterDropReason
{
    /// <summary>The model's catalog does not declare the parameter.</summary>
    NotSupported,

    /// <summary>The model declares the parameter, but no allowed value is close enough.</summary>
    NoAllowedValue,

    /// <summary>The model's parameters are unknown (no catalog entry or an old cache), so nothing can be checked.</summary>
    CatalogUnknown,
}
