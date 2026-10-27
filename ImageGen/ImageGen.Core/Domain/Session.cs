namespace ImageGen.Core.Domain;

public sealed class Session
{
    public const string NewSessionTitle = "New session";
    public const int MaxTitleLength = 40;

    public required Guid Id { get; set; }

    public required string Title { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }

    public required SessionSettings Settings { get; set; }
}
