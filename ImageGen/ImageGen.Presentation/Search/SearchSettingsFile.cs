using System.Text.Json;
using ImageGen.Core.Platform;
using ImageGen.Core.Search;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Search;

/// <summary>Stores the user's semantic-search opt-in in the data folder.</summary>
public sealed class SearchSettingsFile : ISemanticSearchSettingsStore
{
    public const string FileName = "search.json";

    private readonly string _path;
    private readonly ILogger<SearchSettingsFile> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool? _enabled;

    public SearchSettingsFile(IAppPaths paths, ILogger<SearchSettingsFile> logger)
    {
        _path = Path.Combine(paths.DataDirectory, FileName);
        _logger = logger;
    }

    public async Task<bool> GetEnabledAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _enabled ??= Load();
            return _enabled.Value;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _enabled ??= Load();
            if (_enabled.Value == enabled)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path, Serialize(enabled), cancellationToken);
            _enabled = enabled;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static (bool Enabled, string? Warning) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (false, null);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (false, $"{FileName} must contain a JSON object; semantic search is disabled.");
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name != "enabled")
                {
                    return (false, $"{FileName} contains an unknown field; semantic search is disabled.");
                }
            }

            if (!root.TryGetProperty("enabled", out var value))
            {
                return (false, null);
            }

            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return (false, $"{FileName} has an invalid enabled value; semantic search is disabled.");
            }

            return (value.GetBoolean(), null);
        }
        catch (JsonException)
        {
            return (false, $"{FileName} is not valid JSON; semantic search is disabled.");
        }
    }

    public static string Serialize(bool enabled) => JsonSerializer.Serialize(new { enabled });

    private bool Load()
    {
        if (!File.Exists(_path))
        {
            return false;
        }

        var (enabled, warning) = Parse(File.ReadAllText(_path));
        if (warning is not null)
        {
            _logger.LogWarning("{Warning}", warning);
        }

        return enabled;
    }
}
