using System.Text.Json.Serialization;
using ImageGen.Core.Domain.Sessions;

namespace ImageGen.Core.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SessionSettings))]
public sealed partial class CoreJsonContext : JsonSerializerContext
{
}
