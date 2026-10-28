using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Services;

/// <summary>How many reference images a model takes in one request, from its catalog (task 018, F4.5).</summary>
public static class ReferenceImageLimit
{
    /// <summary>Catalog name of the parameter that holds the limit.</summary>
    public const string ParameterName = "input_references";

    /// <summary>The parameter's maximum; 0 when the model does not declare it or the catalog is unknown.</summary>
    public static int Of(ImageModel? model)
    {
        var parameter = model?.Parameters?.FirstOrDefault(
            p => p.Kind == ParameterKind.Range && string.Equals(p.Name, ParameterName, StringComparison.OrdinalIgnoreCase));
        return parameter?.Max is { } max && max > 0 ? (int)max : 0;
    }
}
