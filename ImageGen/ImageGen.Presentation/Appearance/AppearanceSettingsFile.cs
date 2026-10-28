using System.Text.Json;

namespace ImageGen.Presentation.Appearance;

/// <summary>
/// Reads and writes <c>appearance.json</c>. A broken or unknown value falls back to the default for that field
/// and comes back as a warning; nothing here throws.
/// </summary>
public static class AppearanceSettingsFile
{
    public const string FileName = "appearance.json";

    public static (AppearanceSettings Settings, string? Warning) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (AppearanceSettings.Default, null);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (AppearanceSettings.Default, $"{FileName} must contain a JSON object; using the defaults.");
            }

            var warnings = new List<string>();

            var style = ReadEnum(root, "style", AppearanceStyle.Quiet, warnings);
            var theme = ReadEnum(root, "theme", AppearanceTheme.System, warnings);

            return (new AppearanceSettings(style, theme), warnings.Count == 0 ? null : string.Join(" ", warnings));
        }
        catch (JsonException ex)
        {
            return (AppearanceSettings.Default, $"{FileName} is not valid JSON ({ex.Message}); using the defaults.");
        }
    }

    public static string Serialize(AppearanceSettings settings) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["style"] = settings.Style.ToString().ToLowerInvariant(),
            ["theme"] = settings.Theme.ToString().ToLowerInvariant(),
        });

    private static TEnum ReadEnum<TEnum>(JsonElement root, string property, TEnum fallback, List<string> warnings)
        where TEnum : struct, Enum
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return fallback;
        }

        var text = value.GetString() ?? string.Empty;
        if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        var fallbackName = fallback.ToString().ToLowerInvariant();
        warnings.Add($"{FileName}: unknown {property} '{text}'; using {fallbackName}.");
        return fallback;
    }
}
