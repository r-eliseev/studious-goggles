namespace ImageGen.Core.Providers;

/// <param name="Role">"system", "user" or "assistant".</param>
/// <param name="ImageDataUrls">Images as data URLs (<c>data:image/png;base64,...</c>); only for "user" messages.</param>
public sealed record AssistantChatMessage(string Role, string Text, IReadOnlyList<string> ImageDataUrls)
{
    public const string SystemRole = "system";
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public static AssistantChatMessage System(string text) => new(SystemRole, text, []);

    public static AssistantChatMessage User(string text) => new(UserRole, text, []);

    public static AssistantChatMessage Assistant(string text) => new(AssistantRole, text, []);
}
