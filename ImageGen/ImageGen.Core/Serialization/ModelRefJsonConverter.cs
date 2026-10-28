using System.Text.Json;
using System.Text.Json.Serialization;
using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Serialization;

/// <summary>Reads and writes a <see cref="ModelRef"/> as its <c>provider:model</c> text.</summary>
public sealed class ModelRefJsonConverter : JsonConverter<ModelRef>
{
    public override ModelRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A model reference must be a string.");
        }

        var text = reader.GetString() ?? string.Empty;
        try
        {
            return ModelRef.Parse(text);
        }
        catch (ArgumentException ex)
        {
            throw new JsonException(ex.Message, ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, ModelRef value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
