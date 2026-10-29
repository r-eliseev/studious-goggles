using System.Text.RegularExpressions;
using ImageGen.Core.Http;

namespace ImageGen.Core.Diagnostics;

/// <summary>
/// What the trace keeps of the HTTP traffic. Headers are an allowlist: anything else (the key is in <c>Authorization</c> or
/// <c>x-api-key</c>) is dropped, not masked. Pictures inside a body (data URLs and <c>b64_json</c> fields) become blobs and a
/// reference to them. The body is not cut: a trace has to hold the whole response to replay it.
/// </summary>
public static partial class TraceRedaction
{
    public static readonly string[] AllowedHeaders = ["Content-Type", "Accept", "User-Agent", "Content-Length"];

    /// <summary>Keeps only the allowed headers, in their order of appearance.</summary>
    public static IReadOnlyDictionary<string, string?> Headers(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        var kept = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in headers)
        {
            if (AllowedHeaders.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                kept[name] = string.Join(", ", values);
            }
        }

        return kept;
    }

    /// <summary>
    /// Replaces every base64 picture in <paramref name="body"/> (data URLs of images and <c>b64_json</c> fields) with
    /// <c>blob:&lt;name&gt;</c> and collects the pictures as blobs. The account fields are redacted; nothing else changes.
    /// </summary>
    public static string ExtractImages(string body, string namePrefix, List<TraceBlob> blobs)
    {
        var index = 0;
        var replaced = DataUrl().Replace(body, match => ToBlob(match, namePrefix, ref index, blobs, match.Groups["type"].Value));
        replaced = Base64JsonField().Replace(replaced, match =>
            ToBlob(match, namePrefix, ref index, blobs, "application/octet-stream", prefix: "\"b64_json\":\""));
        return BodyRedactor.RedactAccountFields(replaced);
    }

    private static string ToBlob(
        Match match,
        string namePrefix,
        ref int index,
        List<TraceBlob> blobs,
        string mediaType,
        string prefix = "")
    {
        var data = match.Groups["data"].Value;
        if (!Convert.TryFromBase64String(data, new byte[data.Length], out _))
        {
            return match.Value;
        }

        var name = $"{namePrefix}-{index++}";
        blobs.Add(new TraceBlob(name, Convert.FromBase64String(data), mediaType));
        return prefix.Length == 0 ? "blob:" + name : prefix + "blob:" + name + "\"";
    }

    [GeneratedRegex("data:(?<type>image/[a-zA-Z0-9.+-]+);base64,(?<data>[A-Za-z0-9+/=]+)")]
    private static partial Regex DataUrl();

    [GeneratedRegex("\"b64_json\"\\s*:\\s*\"(?<data>[A-Za-z0-9+/=]+)\"")]
    private static partial Regex Base64JsonField();
}
