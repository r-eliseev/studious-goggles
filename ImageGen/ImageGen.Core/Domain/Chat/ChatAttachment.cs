namespace ImageGen.Core.Domain.Chat;

/// <summary>
/// An image the user attached in a session (task 018). The file lives in the session's attachment folder; the row is
/// linked to a message when the message is sent. <see cref="MessageId"/> is null until then.
/// </summary>
public sealed class ChatAttachment
{
    public required Guid Id { get; set; }

    public required Guid SessionId { get; set; }

    public Guid? MessageId { get; set; }

    /// <summary>Path relative to <c>IAppPaths.DataDirectory</c>.</summary>
    public required string RelativePath { get; set; }

    public required string MediaType { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
