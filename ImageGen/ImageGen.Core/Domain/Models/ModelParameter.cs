namespace ImageGen.Core.Domain.Models;

/// <summary>
/// One parameter a model accepts, as its provider's catalog declares it (name as in the catalog).
/// <see cref="Values"/> is set for <see cref="ParameterKind.Enum"/> only, in catalog order.
/// </summary>
public sealed record ModelParameter(string Name, ParameterKind Kind, IReadOnlyList<string> Values, double? Min = null, double? Max = null);
