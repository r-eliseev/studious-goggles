using ImageGen.Core.Platform;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Appearance;

/// <summary>Holds the current appearance and saves changes to <c>appearance.json</c> in the data folder.</summary>
public sealed class AppearanceService
{
    private readonly string _path;
    private readonly ILogger<AppearanceService> _logger;
    private AppearanceSettings? _current;

    public AppearanceService(IAppPaths paths, ILogger<AppearanceService> logger)
    {
        _path = Path.Combine(paths.DataDirectory, AppearanceSettingsFile.FileName);
        _logger = logger;
    }

    /// <summary>Raised after a save that changed the value; not raised for the same value.</summary>
    public event Action<AppearanceSettings>? Changed;

    public AppearanceSettings Current => _current ??= Load();

    public async Task SaveAsync(AppearanceSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings == Current)
        {
            return;
        }

        await File.WriteAllTextAsync(_path, AppearanceSettingsFile.Serialize(settings), cancellationToken);
        _current = settings;
        Changed?.Invoke(settings);
    }

    private AppearanceSettings Load()
    {
        if (!File.Exists(_path))
        {
            return AppearanceSettings.Default;
        }

        var (settings, warning) = AppearanceSettingsFile.Parse(File.ReadAllText(_path));
        if (warning is not null)
        {
            _logger.LogWarning("{Warning}", warning);
        }

        return settings;
    }
}
