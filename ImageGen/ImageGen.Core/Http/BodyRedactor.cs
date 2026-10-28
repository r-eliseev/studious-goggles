using System.Text.RegularExpressions;

namespace ImageGen.Core.Http;

/// <summary>Makes a request or response body safe and short enough for a log line.</summary>
internal static partial class BodyRedactor
{
    public const int MaxLength = 4096;

    /// <summary>Replaces image payloads (JSON <c>b64_json</c> fields and data URLs) and cuts the text to <see cref="MaxLength"/>.</summary>
    public static string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var redacted = Base64JsonField().Replace(text, match =>
            $"\"{match.Groups["name"].Value}\":\"<base64, {DecodedBytes(match.Groups["data"].Value)} bytes>\"");
        redacted = DataUrl().Replace(redacted, match =>
            $"<data url, {match.Groups["type"].Value}, {DecodedBytes(match.Groups["data"].Value)} bytes>");

        if (redacted.Length <= MaxLength)
        {
            return redacted;
        }

        return $"{redacted[..MaxLength]}… (truncated, {redacted.Length} chars in total)";
    }

    /// <summary>Size in bytes of a base64 text, without the padding characters.</summary>
    private static long DecodedBytes(string base64)
    {
        var padding = base64.EndsWith("==", StringComparison.Ordinal) ? 2 : base64.EndsWith('=') ? 1 : 0;
        return base64.Length * 3L / 4 - padding;
    }

    [GeneratedRegex("\"(?<name>b64_json)\"\\s*:\\s*\"(?<data>[A-Za-z0-9+/=\\r\\n]+)\"")]
    private static partial Regex Base64JsonField();

    [GeneratedRegex("data:(?<type>[a-zA-Z0-9.+-]+/[a-zA-Z0-9.+-]+);base64,(?<data>[A-Za-z0-9+/=]+)")]
    private static partial Regex DataUrl();
}
