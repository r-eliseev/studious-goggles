using System.Text.Json;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Serialization;

namespace ImageGen.Core.Tests.Domain;

public class SessionSettingsTests
{
    [Fact]
    public void CreateDefault_ReturnsDefaultModelAndOneRun()
    {
        var settings = SessionSettings.CreateDefault();

        Assert.Equal([new ModelRef(ProviderIds.OpenRouter, "black-forest-labs/flux.2-klein-4b")], settings.ImageModels);
        Assert.Equal(1, settings.RunsPerModel);
        Assert.Equal(new ModelRef(ProviderIds.OpenRouter, "anthropic/claude-haiku-5.5"), settings.AssistantModel);
    }

    [Fact]
    public void Serialize_ThenDeserialize_KeepsValues()
    {
        var settings = new SessionSettings
        {
            ImageModels = [new ModelRef("openrouter", "a"), new ModelRef("fal", "b")],
            RunsPerModel = 3,
            AssistantModel = new ModelRef("openrouter", "x"),
        };

        var json = JsonSerializer.Serialize(settings, CoreJsonContext.Default.SessionSettings);
        var restored = JsonSerializer.Deserialize(json, CoreJsonContext.Default.SessionSettings);

        Assert.NotNull(restored);
        Assert.Equal(settings.ImageModels, restored.ImageModels);
        Assert.Equal(settings.RunsPerModel, restored.RunsPerModel);
        Assert.Equal(settings.AssistantModel, restored.AssistantModel);
    }

    [Fact]
    public void Serialize_UsesLegacyPropertyNames()
    {
        var json = JsonSerializer.Serialize(SessionSettings.CreateDefault(), CoreJsonContext.Default.SessionSettings);

        Assert.Contains("\"imageModelIds\"", json);
        Assert.Contains("\"runsPerModel\"", json);
        Assert.Contains("\"assistantModelId\"", json);
        Assert.Contains("\"openrouter:black-forest-labs/flux.2-klein-4b\"", json);
    }
}
