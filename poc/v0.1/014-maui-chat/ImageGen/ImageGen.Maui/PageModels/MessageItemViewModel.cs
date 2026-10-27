using ImageGen.Core.Domain.Chat;
using ImageGen.Maui.Resources.Strings;

namespace ImageGen.Maui.PageModels;

public sealed class MessageItemViewModel(ChatMessage message)
{
    public string RoleText { get; } = message.Role == ChatRole.User ? AppResources.UserRole : AppResources.AssistantRole;

    public string Text { get; } = message.Text;

    public bool IsUser { get; } = message.Role == ChatRole.User;

    public bool IsError { get; } = message.IsError;
}
