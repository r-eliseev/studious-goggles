namespace ImageGen.Core.Domain.Models;

public sealed record EmbeddingModel
{
    public required string ProviderId { get; set; }

    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public int? ContextLength { get; set; }

    public decimal? PricePerTokenUsd { get; set; }

    public required DateTimeOffset FetchedAt { get; set; }
}
