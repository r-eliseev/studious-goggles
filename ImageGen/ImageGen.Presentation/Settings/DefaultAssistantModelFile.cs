using System.Text.Json;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Settings;

/// <summary>Persists the assistant model used when creating new sessions.</summary>
public sealed class DefaultAssistantModelFile : IDefaultAssistantModel
{
    public const string FileName = "defaults.json";
    private static readonly ModelRef BuiltIn = new(ProviderIds.OpenRouter, SessionSettings.DefaultAssistantModelId);
    private readonly string path;
    private readonly ILogger<DefaultAssistantModelFile> logger;

    public DefaultAssistantModelFile(IAppPaths paths, ILogger<DefaultAssistantModelFile> logger)
    {
        path = Path.Combine(paths.DataDirectory, FileName);
        this.logger = logger;
        Current = Read();
    }

    public ModelRef Current { get; private set; }

    public async Task SaveAsync(ModelRef model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ProviderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(model.ModelId);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(model), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
            Current = model;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (IOException exception)
                {
                    logger.LogWarning("Could not remove a temporary defaults file ({ExceptionType}).", exception.GetType().Name);
                }
            }
        }
    }

    private ModelRef Read()
    {
        if (!File.Exists(path))
        {
            return BuiltIn;
        }

        try
        {
            var model = JsonSerializer.Deserialize<ModelRef>(File.ReadAllText(path));
            if (model is not null && !string.IsNullOrWhiteSpace(model.ProviderId) && !string.IsNullOrWhiteSpace(model.ModelId))
            {
                return model;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning("Could not read defaults.json ({ExceptionType}); using built-in assistant model.", exception.GetType().Name);
        }

        return BuiltIn;
    }
}
