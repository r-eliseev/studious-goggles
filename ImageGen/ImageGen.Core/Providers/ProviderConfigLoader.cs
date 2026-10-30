using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ImageGen.Core.Providers;

/// <summary>
/// Reads the provider config: the built-in file (embedded resource) and the optional user file in the data folder.
/// A user entry with the same id replaces the built-in one; other user entries are added.
/// The file never contains keys: unknown fields are rejected by name.
/// </summary>
public static class ProviderConfigLoader
{
    /// <summary>Name of the user config file inside <c>IAppPaths.DataDirectory</c>.</summary>
    public const string UserFileName = "providers.json";

    private const string BuiltInResourceName = "ImageGen.Core.Providers.builtin-providers.json";
    private const int DefaultTimeoutSeconds = 180;
    private static readonly Regex SecretLikeName = new("key|secret|token|password", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> KnownFields = new(StringComparer.Ordinal)
    {
        "id", "kind", "displayName", "baseAddress", "timeoutSeconds", "imageModels", "imageResponseFormat", "imageOutputFormat",
    };

    /// <summary>Reads the user config from the data folder; null when the file does not exist.</summary>
    public static string? ReadUserJson(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var path = Path.Combine(dataDirectory, UserFileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>Reads the built-in provider file shipped with the assembly.</summary>
    public static string ReadBuiltInJson()
    {
        using var stream = typeof(ProviderConfigLoader).Assembly.GetManifestResourceStream(BuiltInResourceName)
            ?? throw new ProviderConfigException("The built-in provider config is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Builds the registry from the built-in file and the optional user file text.</summary>
    /// <exception cref="ProviderConfigException">Any file or entry is invalid.</exception>
    public static ProviderRegistry Build(string builtInJson, string? userJson)
    {
        ArgumentNullException.ThrowIfNull(builtInJson);
        var builtIn = Parse(builtInJson);
        EnsureUniqueIds(builtIn);

        var merged = builtIn.ToList();
        if (!string.IsNullOrWhiteSpace(userJson))
        {
            var user = Parse(userJson);
            EnsureUniqueIds(user);
            foreach (var provider in user)
            {
                var index = merged.FindIndex(p => p.Id == provider.Id);
                if (index >= 0)
                {
                    merged[index] = provider;
                }
                else
                {
                    merged.Add(provider);
                }
            }
        }

        return new ProviderRegistry(merged);
    }

    /// <summary>Parses one config file. Checks the JSON shape and field values, not the cross-entry rules.</summary>
    /// <exception cref="ProviderConfigException">The JSON or a field is invalid.</exception>
    public static IReadOnlyList<ProviderConfig> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ProviderConfigException($"The provider config is not valid JSON: {ex.Message}", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("providers", out var array)
                || array.ValueKind != JsonValueKind.Array)
            {
                throw new ProviderConfigException("The provider config must be an object with a 'providers' array.");
            }

            var result = new List<ProviderConfig>();
            var index = 0;
            foreach (var entry in array.EnumerateArray())
            {
                result.Add(ParseProvider(entry, index));
                index++;
            }

            return result;
        }
    }

    private static ProviderConfig ParseProvider(JsonElement entry, int index)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw new ProviderConfigException($"Provider entry #{index + 1} must be an object.");
        }

        foreach (var property in entry.EnumerateObject())
        {
            if (KnownFields.Contains(property.Name))
            {
                continue;
            }

            var hint = SecretLikeName.IsMatch(property.Name)
                ? " Keys and passwords are not stored in the config; keep them in the app."
                : string.Empty;
            throw new ProviderConfigException($"Provider entry #{index + 1}: unknown field '{property.Name}'.{hint}");
        }

        var id = ReadString(entry, "id", $"entry #{index + 1}");
        var label = $"'{id}'";
        var kind = ParseKind(ReadString(entry, "kind", label), label);
        var displayName = ReadString(entry, "displayName", label);
        var address = ReadString(entry, "baseAddress", label);
        var timeoutSeconds = ReadOptionalInt(entry, "timeoutSeconds", DefaultTimeoutSeconds, label);
        var imageResponseFormat = ReadOptionalFormat(entry, "imageResponseFormat", ["base64", "b64_json"], label);
        var imageOutputFormat = ReadOptionalFormat(entry, "imageOutputFormat", ["png", "jpeg"], label);
        if ((imageResponseFormat is not null || imageOutputFormat is not null) && kind != ProviderKind.OpenAiCompatible)
        {
            throw new ProviderConfigException($"Provider {label}: imageResponseFormat and imageOutputFormat are allowed only for kind openAiCompatible.");
        }

        if (!Uri.TryCreate(address.EndsWith('/') ? address : address + "/", UriKind.Absolute, out var baseAddress))
        {
            throw new ProviderConfigException($"Provider {label}: baseAddress must be an absolute URL.");
        }

        return new ProviderConfig(
            id,
            kind,
            displayName,
            baseAddress,
            TimeSpan.FromSeconds(timeoutSeconds),
            ParseImageModels(entry, label),
            imageResponseFormat,
            imageOutputFormat);
    }

    private static IReadOnlyList<ConfiguredImageModel> ParseImageModels(JsonElement entry, string label)
    {
        if (!entry.TryGetProperty("imageModels", out var array))
        {
            return [];
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new ProviderConfigException($"Provider {label}: imageModels must be an array.");
        }

        var models = new List<ConfiguredImageModel>();
        foreach (var item in array.EnumerateArray())
        {
            var id = ReadString(item, "id", $"{label} imageModels entry");
            var name = item.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()!
                : id;
            var supportsSeed = item.TryGetProperty("supportsSeed", out var seedElement)
                && seedElement.ValueKind == JsonValueKind.True;
            var maxImages = ReadOptionalInt(item, "maxImagesPerRequest", 1, $"{label} model '{id}'");
            models.Add(new ConfiguredImageModel(id, name, supportsSeed, maxImages));
        }

        return models;
    }

    private static ProviderKind ParseKind(string text, string label) => text switch
    {
        "openRouter" => ProviderKind.OpenRouter,
        "openAiCompatible" => ProviderKind.OpenAiCompatible,
        "anthropicCompatible" => ProviderKind.AnthropicCompatible,
        "fal" => ProviderKind.Fal,
        _ => throw new ProviderConfigException(
            $"Provider {label}: unknown kind '{text}'. Known kinds: openRouter, openAiCompatible, anthropicCompatible, fal."),
    };

    private static string ReadString(JsonElement element, string name, string label)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new ProviderConfigException($"Provider {label}: '{name}' is required and must be a string.");
        }

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ProviderConfigException($"Provider {label}: '{name}' is empty.");
        }

        return text.Trim();
    }

    private static string? ReadOptionalFormat(JsonElement element, string name, IReadOnlyCollection<string> allowed, string label)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ProviderConfigException($"Provider {label}: '{name}' must be a string.");
        }

        var text = value.GetString();
        if (string.IsNullOrEmpty(text) || !allowed.Contains(text))
        {
            throw new ProviderConfigException($"Provider {label}: '{name}' has an unsupported value.");
        }

        return text;
    }

    private static int ReadOptionalInt(JsonElement element, string name, int defaultValue, string label)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            throw new ProviderConfigException($"Provider {label}: '{name}' must be a whole number.");
        }

        return number;
    }

    private static void EnsureUniqueIds(IEnumerable<ProviderConfig> providers)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            if (!seen.Add(provider.Id))
            {
                throw new ProviderConfigException($"Provider id '{provider.Id}' is defined twice.");
            }
        }
    }
}
