namespace ImageGen.Core.Domain;

public sealed class Run
{
    public required Guid Id { get; set; }

    public required Guid SessionId { get; set; }

    public required string Prompt { get; set; }

    public required RunOrigin Origin { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
