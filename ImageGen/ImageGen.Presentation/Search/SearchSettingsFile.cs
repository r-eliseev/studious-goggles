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
    private string? _model;

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
            EnsureLoaded();
            return _enabled.GetValueOrDefault();
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
            EnsureLoaded();
            if (_enabled.GetValueOrDefault() == enabled)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path, Serialize(enabled, _model ?? SemanticSearchModels.Default), cancellationToken);
            _enabled = enabled;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetModelAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureLoaded();
            return _model!;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetModelAsync(string modelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureLoaded();
            if (_model == modelId)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path, Serialize(_enabled!.Value, modelId), cancellationToken);
            _model = modelId;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static (bool Enabled, string Model, string? Warning) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (false, SemanticSearchModels.Default, null);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (false, SemanticSearchModels.Default, $"{FileName} must contain a JSON object; semantic search is disabled.");
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is not ("enabled" or "model"))
                {
                    return (false, SemanticSearchModels.Default, $"{FileName} contains an unknown field; semantic search is disabled.");
                }
            }

            var enabled = false;
            if (root.TryGetProperty("enabled", out var value))
            {
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return (false, SemanticSearchModels.Default, $"{FileName} has an invalid enabled value; semantic search is disabled.");
                }

                enabled = value.GetBoolean();
            }

            var model = SemanticSearchModels.Default;
            string? warning = null;
            if (!root.TryGetProperty("model", out var modelValue))
            {
                warning = $"{FileName} has no model; semantic search will use {SemanticSearchModels.Default}.";
            }
            else if (modelValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(modelValue.GetString()))
            {
                model = modelValue.GetString()!;
            }
            else
            {
                warning = $"{FileName} has an invalid model; semantic search will use {SemanticSearchModels.Default}.";
            }

            return (enabled, model, warning);
        }
        catch (JsonException)
        {
            return (false, SemanticSearchModels.Default, $"{FileName} is not valid JSON; semantic search is disabled.");
        }
    }

    public static string Serialize(bool enabled, string model) => JsonSerializer.Serialize(new { enabled, model });

    private bool Load()
    {
        if (!File.Exists(_path))
        {
            _model = SemanticSearchModels.Default;
            return false;
        }

        var (enabled, model, warning) = Parse(File.ReadAllText(_path));
        if (warning is not null)
        {
            _logger.LogWarning("{Warning}", warning);
        }

        _model = model;
        return enabled;
    }

    private void EnsureLoaded()
    {
        if (_enabled is not null)
        {
            return;
        }

        if (File.Exists(_path))
        {
            _enabled = Load();
        }
        else
        {
            _enabled = false;
            _model = SemanticSearchModels.Default;
        }
    }
}
