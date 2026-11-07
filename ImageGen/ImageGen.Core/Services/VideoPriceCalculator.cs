using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Services;

public static class VideoPriceCalculator
{
    public static decimal? Estimate(VideoModel model, int durationSeconds, string? resolution, bool generateAudio)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationSeconds);

        var prices = model.PriceSkus;
        if (prices.Keys.Any(key => key.StartsWith("video_tokens", StringComparison.Ordinal)))
        {
            return null;
        }

        var normalizedResolution = string.IsNullOrWhiteSpace(resolution) ? null : resolution.ToLowerInvariant();
        decimal? estimate = FindDurationRate(prices, normalizedResolution, generateAudio) is { } durationRate
            ? durationRate * durationSeconds
            : FindCentRate(prices, normalizedResolution) is { } centRate
                ? centRate * durationSeconds / 100m
                : null;

        if (estimate is null)
        {
            return null;
        }

        if (prices.TryGetValue("minimum_cents_per_generation", out var minimumCents))
        {
            estimate = Math.Max(estimate.Value, minimumCents / 100m);
        }

        return estimate;
    }

    private static decimal? FindDurationRate(
        IReadOnlyDictionary<string, decimal> prices,
        string? resolution,
        bool generateAudio)
    {
        var audioSuffix = generateAudio ? "_with_audio" : "_without_audio";
        var candidates = new List<string>(4);
        if (resolution is not null)
        {
            candidates.Add($"duration_seconds{audioSuffix}_{resolution}");
        }

        candidates.Add($"duration_seconds{audioSuffix}");
        if (resolution is not null)
        {
            candidates.Add($"duration_seconds_{resolution}");
        }

        candidates.Add("duration_seconds");
        foreach (var candidate in candidates)
        {
            if (prices.TryGetValue(candidate, out var rate))
            {
                return rate;
            }
        }

        return null;
    }

    private static decimal? FindCentRate(IReadOnlyDictionary<string, decimal> prices, string? resolution)
    {
        var candidates = new List<string>(3);
        if (resolution is not null)
        {
            candidates.Add($"cents_per_video_output_second_{resolution}");
            candidates.Add($"cents_per_second_output_{resolution}");
        }

        candidates.Add("cents_per_second_output");
        foreach (var candidate in candidates)
        {
            if (prices.TryGetValue(candidate, out var rate))
            {
                return rate;
            }
        }

        return null;
    }
}
