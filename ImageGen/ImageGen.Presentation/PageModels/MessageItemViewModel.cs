using ImageGen.Core.Domain.Chat;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

/// <summary>One chat message row. Messages do not change after they are saved.</summary>
public sealed class MessageItemViewModel
{
    public MessageItemViewModel(ChatMessage message)
        : this(message, (message ?? throw new ArgumentNullException(nameof(message))).Text)
    {
    }

    /// <param name="message">Saved message; its id, role and error flag are used.</param>
    /// <param name="text">Text to show, when it differs from the saved one (see <see cref="RunMessageText"/>).</param>
    public MessageItemViewModel(ChatMessage message, string text)
    {
        ArgumentNullException.ThrowIfNull(message);
        Id = message.Id;
        IsUser = message.Role == ChatRole.User;
        RoleText = IsUser ? AppResources.UserRole : AppResources.AssistantRole;
        Text = text;
        IsError = message.IsError;
    }

    public Guid Id { get; }

    public bool IsUser { get; }

    public string RoleText { get; }

    public string Text { get; }

    /// <summary>True for an assistant message that reports an error.</summary>
    public bool IsError { get; }
}
