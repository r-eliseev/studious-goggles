using System.Text.Json;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Serialization;

namespace ImageGen.Core.Tests.Domain;

public class SessionSettingsTests
{
    [Fact]
    public void CreateDefault_ReturnsDefaultModelAndOneRun()
    {
        var settings = SessionSettings.CreateDefault();

        Assert.Equal(["black-forest-labs/flux.2-klein-4b"], settings.ImageModelIds);
        Assert.Equal(1, settings.RunsPerModel);
        Assert.Equal("google/gemma-4-31b-it:free", settings.AssistantModelId);
    }

    [Fact]
    public void Serialize_ThenDeserialize_KeepsValues()
    {
        var settings = new SessionSettings
        {
            ImageModelIds = ["a", "b"],
            RunsPerModel = 3,
            AssistantModelId = "x",
        };

        var json = JsonSerializer.Serialize(settings, CoreJsonContext.Default.SessionSettings);
        var restored = JsonSerializer.Deserialize(json, CoreJsonContext.Default.SessionSettings);

        Assert.NotNull(restored);
        Assert.Equal(settings.ImageModelIds, restored.ImageModelIds);
        Assert.Equal(settings.RunsPerModel, restored.RunsPerModel);
        Assert.Equal(settings.AssistantModelId, restored.AssistantModelId);
    }

    [Fact]
    public void Serialize_UsesCamelCaseNames()
    {
        var json = JsonSerializer.Serialize(SessionSettings.CreateDefault(), CoreJsonContext.Default.SessionSettings);

        Assert.Contains("\"imageModelIds\"", json);
        Assert.Contains("\"runsPerModel\"", json);
        Assert.Contains("\"assistantModelId\"", json);
    }
}
