using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ImageGen.Core.Providers;
using ImageGen.Core.Serialization;

namespace ImageGen.Core.Domain.Models;

/// <summary>
/// A model of one provider. Stored and shown as <c>provider:model</c>. Text without a provider prefix
/// (saved before provider ids existed) is an OpenRouter model. The model id may contain colons (<c>:free</c>).
/// </summary>
[JsonConverter(typeof(ModelRefJsonConverter))]
public sealed record ModelRef(string ProviderId, string ModelId)
{
    private static readonly Regex ProviderPrefix = new("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant);

    public static ModelRef Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var trimmed = text.Trim();
        if (trimmed.StartsWith(':'))
        {
            throw new ArgumentException($"Provider id is empty in '{trimmed}'.", nameof(text));
        }

        var colon = trimmed.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && ProviderPrefix.IsMatch(trimmed[..colon]))
        {
            var provider = trimmed[..colon];
            var model = trimmed[(colon + 1)..];
            if (model.Length == 0)
            {
                throw new ArgumentException($"Model id is empty in '{trimmed}'.", nameof(text));
            }

            return new ModelRef(provider, model);
        }

        return new ModelRef(ProviderIds.OpenRouter, trimmed);
    }

    public override string ToString() => $"{ProviderId}:{ModelId}";
}
