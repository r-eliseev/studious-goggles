namespace ImageGen.Core.Domain.Models;

public sealed record VideoModel
{
    public required string ProviderId { get; set; }

    public required string Id { get; set; }

    public required string Name { get; set; }

    public required IReadOnlyList<int> Durations { get; set; }

    public required IReadOnlyList<string> Resolutions { get; set; }

    public required IReadOnlyList<string> AspectRatios { get; set; }

    public required IReadOnlyList<string> FrameImages { get; set; }

    public bool? GeneratesAudio { get; set; }

    public bool? SupportsSeed { get; set; }

    public required IReadOnlyDictionary<string, decimal> PriceSkus { get; set; }

    public required DateTimeOffset FetchedAt { get; set; }
}
