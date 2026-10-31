using System.Text.Json.Serialization;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Domain.Sessions;

public sealed class SessionSettings
{
    public const string DefaultImageModelId = "black-forest-labs/flux.2-klein-4b";
    public const string DefaultAssistantModelId = "anthropic/claude-haiku-5.5";
    public const int MinRunsPerModel = 1;
    public const int MaxRunsPerModel = 10;

    /// <summary>Image models of the session. Stored under the legacy name <c>imageModelIds</c>.</summary>
    [property: JsonPropertyName("imageModelIds")]
    public List<ModelRef> ImageModels { get; set; } = [];

    public int RunsPerModel { get; set; } = MinRunsPerModel;

    /// <summary>Parameters asked for every run of the session. Changing the models does not change them.</summary>
    public GenerationParameters Parameters { get; set; } = GenerationParameters.Empty;

    /// <summary>Assistant chat model. Stored under the legacy name <c>assistantModelId</c>.</summary>
    [property: JsonPropertyName("assistantModelId")]
    public ModelRef AssistantModel { get; set; } = new(ProviderIds.OpenRouter, DefaultAssistantModelId);

    public static SessionSettings CreateDefault() => CreateDefault(new ModelRef(ProviderIds.OpenRouter, DefaultAssistantModelId));

    public static SessionSettings CreateDefault(ModelRef assistantModel)
    {
        ArgumentNullException.ThrowIfNull(assistantModel);
        return new SessionSettings
        {
            ImageModels = [new ModelRef(ProviderIds.OpenRouter, DefaultImageModelId)],
            RunsPerModel = MinRunsPerModel,
            AssistantModel = assistantModel,
        };
    }
}
