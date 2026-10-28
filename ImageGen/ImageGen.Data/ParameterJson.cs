using System.Text.Json;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Serialization;

namespace ImageGen.Data;

/// <summary>JSON of the parameter columns. Goes through <see cref="CoreJsonContext"/> only (source generation).</summary>
internal static class ParameterJson
{
    public static string? Catalog(IReadOnlyList<ModelParameter>? parameters) =>
        parameters is null ? null : JsonSerializer.Serialize(parameters.ToList(), CoreJsonContext.Default.ListModelParameter);

    /// <summary>Null when the column is NULL (the cache row predates parameters).</summary>
    public static IReadOnlyList<ModelParameter>? ReadCatalog(string? json) =>
        json is null ? null : JsonSerializer.Deserialize(json, CoreJsonContext.Default.ListModelParameter);

    public static string Run(GenerationParameters parameters) =>
        JsonSerializer.Serialize(parameters, CoreJsonContext.Default.GenerationParameters);

    /// <summary>NULL (an old run) reads as "no parameters asked for".</summary>
    public static GenerationParameters ReadRun(string? json) =>
        json is null ? GenerationParameters.Empty : JsonSerializer.Deserialize(json, CoreJsonContext.Default.GenerationParameters) ?? GenerationParameters.Empty;

    public static string Outcomes(IReadOnlyList<ParameterOutcome> outcomes) =>
        JsonSerializer.Serialize(outcomes.ToList(), CoreJsonContext.Default.ListParameterOutcome);

    /// <summary>NULL (an old generation) reads as no outcomes.</summary>
    public static IReadOnlyList<ParameterOutcome> ReadOutcomes(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize(json, CoreJsonContext.Default.ListParameterOutcome) ?? [];
}
