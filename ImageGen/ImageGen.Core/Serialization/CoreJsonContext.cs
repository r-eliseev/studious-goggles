using System.Text.Json.Serialization;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;

namespace ImageGen.Core.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionSettings))]
[JsonSerializable(typeof(GenerationParameters))]
[JsonSerializable(typeof(List<Guid>))]
[JsonSerializable(typeof(List<ModelParameter>))]
[JsonSerializable(typeof(IReadOnlyList<ModelParameter>))]
[JsonSerializable(typeof(List<ParameterOutcome>))]
[JsonSerializable(typeof(IReadOnlyList<ParameterOutcome>))]
public sealed partial class CoreJsonContext : JsonSerializerContext
{
}
