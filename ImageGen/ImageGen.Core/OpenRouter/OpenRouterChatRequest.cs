namespace ImageGen.Core.OpenRouter;

public sealed record OpenRouterChatRequest(
    string Model,
    IReadOnlyList<OpenRouterChatMessage> Messages,
    IReadOnlyList<OpenRouterTool> Tools);
