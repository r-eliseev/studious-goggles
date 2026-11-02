using System.Text.Json;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;

namespace ImageGen.Web.Settings;

public sealed class WebDefaultAssistantModel : IDefaultAssistantModel
{
    private static readonly ModelRef BuiltIn = new(ProviderIds.OpenRouter, SessionSettings.DefaultAssistantModelId);
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public WebDefaultAssistantModel(IAppPaths paths)
    {
        _path = Path.Combine(paths.DataDirectory, "defaults.json");
        Current = Read();
    }

    public ModelRef Current { get; private set; }

    public async Task SaveAsync(ModelRef model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelId);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var temporary = _path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(model), cancellationToken);
            File.Move(temporary, _path, overwrite: true);
            Current = model;
        }
        finally
        {
            _lock.Release();
        }
    }

    private ModelRef Read()
    {
        if (!File.Exists(_path))
        {
            return BuiltIn;
        }

        try
        {
            var model = JsonSerializer.Deserialize<ModelRef>(File.ReadAllText(_path));
            return model is not null && !string.IsNullOrWhiteSpace(model.ProviderId) && !string.IsNullOrWhiteSpace(model.ModelId)
                ? model
                : BuiltIn;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return BuiltIn;
        }
    }
}
