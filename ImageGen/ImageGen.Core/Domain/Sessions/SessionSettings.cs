using System.Text.Json.Serialization;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Domain.Sessions;

public sealed class SessionSettings
{
    public const string DefaultImageModelId = "black-forest-labs/flux.2-klein-4b";
    public const string DefaultAssistantModelId = "google/gemma-4-31b-it:free";
    public const int MinRunsPerModel = 1;
    public const int MaxRunsPerModel = 10;

    /// <summary>Image models of the session. Stored under the legacy name <c>imageModelIds</c>.</summary>
    [property: JsonPropertyName("imageModelIds")]
    public List<ModelRef> ImageModels { get; set; } = [];

    public int RunsPerModel { get; set; } = MinRunsPerModel;

    /// <summary>Assistant chat model. Stored under the legacy name <c>assistantModelId</c>.</summary>
    [property: JsonPropertyName("assistantModelId")]
    public ModelRef AssistantModel { get; set; } = new(ProviderIds.OpenRouter, DefaultAssistantModelId);

    public static SessionSettings CreateDefault() => new()
    {
        ImageModels = [new ModelRef(ProviderIds.OpenRouter, DefaultImageModelId)],
        RunsPerModel = MinRunsPerModel,
        AssistantModel = new ModelRef(ProviderIds.OpenRouter, DefaultAssistantModelId),
    };
}
