using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Chat;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

/// <summary>One chat message row. Messages do not change after they are saved.</summary>
public sealed partial class MessageItemViewModel : ObservableObject
{
    private readonly Func<MessageItemViewModel, Task>? _retry;

    /// <param name="message">Saved message; its id, role and error flag are used.</param>
    /// <param name="text">Text to show, when it differs from the saved one (see <see cref="RunMessageText"/>).</param>
    /// <param name="copy">Copies <paramref name="text"/>.</param>
    /// <param name="retry">Sends the chat's last message again (S2.5); runs only while <see cref="CanRetry"/> is true.</param>
    public MessageItemViewModel(ChatMessage message, string text, CopyTextAction copy, Func<MessageItemViewModel, Task>? retry = null)
    {
        _retry = retry;
        ArgumentNullException.ThrowIfNull(message);
        Id = message.Id;
        IsUser = message.Role == ChatRole.User;
        RoleText = IsUser ? AppResources.UserRole : AppResources.AssistantRole;
        Text = text;
        IsError = message.IsError;
        Copy = copy ?? throw new ArgumentNullException(nameof(copy));
    }

    public Guid Id { get; }

    public bool IsUser { get; }

    public string RoleText { get; }

    public string Text { get; }

    /// <summary>True for an assistant message that reports an error.</summary>
    public bool IsError { get; }

    public CopyTextAction Copy { get; }

    /// <summary>
    /// True only for the last message of the chat when it is an error and nothing is running: the «Try again» button shows
    /// (S2.5, task 077).
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial bool CanRetry { get; set; }

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => _retry?.Invoke(this) ?? Task.CompletedTask;
}
