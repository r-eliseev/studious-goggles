namespace ImageGen.Core.OpenRouter;

/// <param name="Role">"system", "user" or "assistant".</param>
/// <param name="ImageDataUrls">Images as data URLs (<c>data:image/png;base64,...</c>); only for "user" messages.</param>
public sealed record OpenRouterChatMessage(string Role, string Text, IReadOnlyList<string> ImageDataUrls)
{
    public const string SystemRole = "system";
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public static OpenRouterChatMessage System(string text) => new(SystemRole, text, []);

    public static OpenRouterChatMessage User(string text) => new(UserRole, text, []);

    public static OpenRouterChatMessage Assistant(string text) => new(AssistantRole, text, []);
}
