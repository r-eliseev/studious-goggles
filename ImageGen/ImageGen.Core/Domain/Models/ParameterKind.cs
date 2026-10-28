namespace ImageGen.Core.Domain.Models;

/// <summary>How a model parameter takes its value, as the provider's catalog declares it.</summary>
public enum ParameterKind
{
    /// <summary>One of a fixed list of values (<see cref="ModelParameter.Values"/>).</summary>
    Enum,

    /// <summary>A number between <see cref="ModelParameter.Min"/> and <see cref="ModelParameter.Max"/>.</summary>
    Range,

    /// <summary>On or off.</summary>
    Boolean,
}
