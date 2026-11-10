namespace ImageGen.Core.Domain.Models;

public enum AudioOutput
{
    ChatAudio,
    Speech,
}

public sealed record AudioModel
{
    public required string ProviderId { get; init; }

    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required AudioOutput Output { get; init; }

    public decimal? PricePerInputCharacterUsd { get; init; }

    public required DateTimeOffset FetchedAt { get; init; }
}
