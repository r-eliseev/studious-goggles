using System.Globalization;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;

namespace ImageGen.Core.Services;

/// <summary>
/// Decides what is sent for each requested parameter of one model (F4.3). Pure: no I/O and no clock. The result is
/// stored with the generation, and the request to the provider is built from it.
/// </summary>
public static class GenerationParameterPlanner
{
    public const string AspectRatioName = "aspect_ratio";
    public const string ResolutionName = "resolution";
    public const string OutputFormatName = "output_format";
    public const string QualityName = "quality";

    private const string AutoValue = "auto";

    /// <summary>Outcomes for every requested (non-null) parameter, in the order of the names above.</summary>
    public static IReadOnlyList<ParameterOutcome> Plan(GenerationParameters requested, ImageModel? model)
    {
        ArgumentNullException.ThrowIfNull(requested);
        var outcomes = new List<ParameterOutcome>();
        foreach (var (name, value) in Requested(requested))
        {
            outcomes.Add(PlanOne(name, value, model));
        }

        return outcomes;
    }

    private static IEnumerable<(string Name, string Value)> Requested(GenerationParameters requested)
    {
        if (requested.AspectRatio is not null) yield return (AspectRatioName, requested.AspectRatio);
        if (requested.Resolution is not null) yield return (ResolutionName, requested.Resolution);
        if (requested.OutputFormat is not null) yield return (OutputFormatName, requested.OutputFormat);
        if (requested.Quality is not null) yield return (QualityName, requested.Quality);
    }

    private static ParameterOutcome PlanOne(string name, string value, ImageModel? model)
    {
        if (model?.Parameters is null)
        {
            return Dropped(name, value, ParameterDropReason.CatalogUnknown);
        }

        var parameter = model.Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (parameter is null || parameter.Kind != ParameterKind.Enum)
        {
            return Dropped(name, value, ParameterDropReason.NotSupported);
        }

        var exact = parameter.Values.FirstOrDefault(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return new ParameterOutcome(name, ParameterOutcomeKind.Applied, value, exact);
        }

        var nearest = name switch
        {
            AspectRatioName => NearestAspectRatio(parameter.Values, value),
            ResolutionName => NearestResolution(parameter.Values, value),
            _ => null,
        };
        return nearest is null
            ? Dropped(name, value, ParameterDropReason.NoAllowedValue)
            : new ParameterOutcome(name, ParameterOutcomeKind.Replaced, value, nearest);
    }

    /// <summary>Closest "W:H" by the ratio's log distance. "auto" is not a ratio; it is used only when nothing else exists.</summary>
    private static string? NearestAspectRatio(IReadOnlyList<string> values, string requested)
    {
        if (string.Equals(requested, AutoValue, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var target = ParseRatio(requested);
        if (target is null)
        {
            return null;
        }

        string? best = null;
        var bestDistance = double.MaxValue;
        foreach (var value in values)
        {
            var ratio = ParseRatio(value);
            if (ratio is null)
            {
                continue;
            }

            var distance = Math.Abs(Math.Log(ratio.Value) - Math.Log(target.Value));
            if (distance < bestDistance)
            {
                best = value;
                bestDistance = distance;
            }
        }

        if (best is not null)
        {
            return best;
        }

        return values.Any(v => string.Equals(v, AutoValue, StringComparison.OrdinalIgnoreCase)) ? AutoValue : null;
    }

    /// <summary>Closest value by number. "2K" is 2000, "1024" is 1024; the unit of plain numbers is not confirmed (see task 020).</summary>
    private static string? NearestResolution(IReadOnlyList<string> values, string requested)
    {
        var target = ParseResolution(requested);
        if (target is null)
        {
            return null;
        }

        string? best = null;
        var bestDistance = double.MaxValue;
        foreach (var value in values)
        {
            var number = ParseResolution(value);
            if (number is null)
            {
                continue;
            }

            var distance = Math.Abs(number.Value - target.Value);
            if (distance < bestDistance)
            {
                best = value;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static double? ParseRatio(string text)
    {
        var parts = text.Split(':');
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var height)
            || width <= 0 || height <= 0)
        {
            return null;
        }

        return width / height;
    }

    private static double? ParseResolution(string text)
    {
        var trimmed = text.Trim();
        var multiplier = 1.0;
        if (trimmed.EndsWith('K') || trimmed.EndsWith('k'))
        {
            trimmed = trimmed[..^1];
            multiplier = 1000;
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0
            ? number * multiplier
            : null;
    }

    private static ParameterOutcome Dropped(string name, string value, ParameterDropReason reason) =>
        new(name, ParameterOutcomeKind.Dropped, value, null, reason);
}
