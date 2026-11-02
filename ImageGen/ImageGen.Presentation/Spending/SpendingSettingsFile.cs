using System.Text.Json;
using ImageGen.Core.Platform;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Spending;

public sealed class SpendingSettingsFile
{
    public const string FileName = "spending.json";
    private readonly string _path;
    private readonly ILogger<SpendingSettingsFile> _logger;

    public SpendingSettingsFile(IAppPaths paths, ILogger<SpendingSettingsFile> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);
        _path = Path.Combine(paths.DataDirectory, FileName);
        _logger = logger;
    }

    public async Task<SpendingSettings> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return SpendingSettings.Default;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_path, cancellationToken);
            var (settings, warning) = Parse(json);
            if (warning is not null)
            {
                _logger.LogWarning("{Warning}", warning);
            }

            return settings;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Could not read {FileName}; using disabled spending thresholds.", FileName);
            _logger.LogDebug(ex, "Could not parse {FileName}.", FileName);
            return SpendingSettings.Default;
        }
        catch (IOException ex)
        {
            _logger.LogWarning("Could not read {FileName}; using disabled spending thresholds.", FileName);
            _logger.LogDebug(ex, "Could not read {FileName}.", FileName);
            return SpendingSettings.Default;
        }
    }

    public async Task SaveAsync(SpendingSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, Serialize(settings), cancellationToken);
    }

    public static (SpendingSettings Settings, string? Warning) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (SpendingSettings.Default, null);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (SpendingSettings.Default, $"{FileName} must contain a JSON object; thresholds are disabled.");
            }

            var warnings = new List<string>();
            var monthly = ReadNonNegative(document.RootElement, "monthlyLimitUsd", warnings);
            var expensive = ReadNonNegative(document.RootElement, "expensiveRunThresholdUsd", warnings);
            return (new SpendingSettings(monthly, expensive), warnings.Count == 0 ? null : string.Join(" ", warnings));
        }
        catch (JsonException ex)
        {
            return (SpendingSettings.Default, $"{FileName} is invalid JSON ({ex.Message}); thresholds are disabled.");
        }
    }

    public static string Serialize(SpendingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        return JsonSerializer.Serialize(new Dictionary<string, decimal?>
        {
            ["monthlyLimitUsd"] = settings.MonthlyLimitUsd,
            ["expensiveRunThresholdUsd"] = settings.ExpensiveRunThresholdUsd,
        });
    }

    private static decimal? ReadNonNegative(JsonElement root, string property, List<string> warnings)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var amount) && amount >= 0)
        {
            return amount;
        }

        warnings.Add($"{FileName}: invalid {property}; that threshold is disabled.");
        return null;
    }

    private static void Validate(SpendingSettings settings)
    {
        if (settings.MonthlyLimitUsd is < 0 || settings.ExpensiveRunThresholdUsd is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Spending thresholds must be non-negative or disabled.");
        }
    }
}
