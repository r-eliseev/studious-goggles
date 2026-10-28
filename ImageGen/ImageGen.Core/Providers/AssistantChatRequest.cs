namespace ImageGen.Core.Providers;

/// <param name="Model">Model id without the provider prefix.</param>
public sealed record AssistantChatRequest(
    string Model,
    IReadOnlyList<AssistantChatMessage> Messages,
    IReadOnlyList<AssistantTool> Tools);
