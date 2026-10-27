namespace ImageGen.Core.Domain;

public sealed class SessionSettings
{
    public const string DefaultImageModelId = "black-forest-labs/flux.2-klein-4b";
    public const string DefaultAssistantModelId = "google/gemma-4-31b-it:free";
    public const int MinRunsPerModel = 1;
    public const int MaxRunsPerModel = 10;

    public List<string> ImageModelIds { get; set; } = [];

    public int RunsPerModel { get; set; } = MinRunsPerModel;

    public string AssistantModelId { get; set; } = DefaultAssistantModelId;

    public static SessionSettings CreateDefault() => new()
    {
        ImageModelIds = [DefaultImageModelId],
        RunsPerModel = MinRunsPerModel,
        AssistantModelId = DefaultAssistantModelId,
    };
}
