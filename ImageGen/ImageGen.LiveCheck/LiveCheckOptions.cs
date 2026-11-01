using System.Globalization;

namespace ImageGen.LiveCheck;

public sealed record LiveCheckOptions(
    IReadOnlyDictionary<string, string> Keys,
    decimal BudgetUsd,
    int MaxRequests,
    IReadOnlySet<string>? Only,
    string OutputDirectory,
    string? OpenAiCompatibleFile)
{
    public static LiveCheckOptions Parse(string[] args, IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        environment ??= Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key is string)
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? "", StringComparer.Ordinal);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(["--openrouter-key", "--fal-key", "--together-key", "--openai-compatible", "--budget-usd", "--max-requests", "--only", "--out", "--openrouter-image-model", "--openrouter-chat-model", "--fal-image-model", "--together-image-model"], StringComparer.OrdinalIgnoreCase);
        var additionalKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            var isProviderKey = name.StartsWith("--provider-key-", StringComparison.OrdinalIgnoreCase);
            if ((!flags.Contains(name) && !isProviderKey) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid or incomplete option '{name}'.");
            }

            var value = args[++i];
            if (!values.TryAdd(name, value))
            {
                throw new ArgumentException($"Option '{name}' was specified more than once.");
            }

            if (isProviderKey)
            {
                var providerId = name["--provider-key-".Length..];
                if (string.IsNullOrWhiteSpace(providerId) || providerId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
                {
                    throw new ArgumentException("Provider key option must use --provider-key-<provider-id>.");
                }

                additionalKeys.Add(providerId, value);
            }
        }

        if (!decimal.TryParse(values.GetValueOrDefault("--budget-usd", "1"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var budget) || budget <= 0)
        {
            throw new ArgumentException("--budget-usd must be a positive decimal number.");
        }

        if (!int.TryParse(values.GetValueOrDefault("--max-requests", "40"), NumberStyles.None, CultureInfo.InvariantCulture, out var maxRequests) || maxRequests <= 0)
        {
            throw new ArgumentException("--max-requests must be a positive integer.");
        }

        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        AddKey("openrouter", "--openrouter-key");
        AddKey("fal", "--fal-key");
        AddKey("together", "--together-key");
        foreach (var provider in new[] { "openai", "anthropic" })
        {
            var value = environment.GetValueOrDefault($"IMAGEGEN_LIVE_{provider.ToUpperInvariant()}_KEY");
            if (!string.IsNullOrWhiteSpace(value))
            {
                keys[provider] = value;
            }
        }
        foreach (var (provider, key) in additionalKeys)
        {
            keys[provider] = key;
        }

        var only = values.TryGetValue("--only", out var onlyValue)
            ? onlyValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)
            : null;
        return new LiveCheckOptions(keys, budget, maxRequests, only, values.GetValueOrDefault("--out", "tmp/live-check"), values.GetValueOrDefault("--openai-compatible"))
        {
            Models = values.Where(pair => pair.Key.EndsWith("-image-model", StringComparison.OrdinalIgnoreCase) || pair.Key.EndsWith("-chat-model", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key[2..^6], pair => pair.Value, StringComparer.Ordinal),
        };

        void AddKey(string provider, string argument)
        {
            var value = values.GetValueOrDefault(argument);
            if (string.IsNullOrWhiteSpace(value))
            {
                value = environment.GetValueOrDefault($"IMAGEGEN_LIVE_{provider.ToUpperInvariant()}_KEY");
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                keys.Add(provider, value);
            }
        }
    }

    public IReadOnlyDictionary<string, string> Models { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
