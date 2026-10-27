namespace ImageGen.Core.Domain;

public sealed class ChatMessage
{
    public required Guid Id { get; set; }

    public required Guid SessionId { get; set; }

    public required ChatRole Role { get; set; }

    public required string Text { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>Run started by this assistant message, if any.</summary>
    public Guid? RunId { get; set; }

    /// <summary>True for assistant messages that report an error.</summary>
    public bool IsError { get; set; }
}
