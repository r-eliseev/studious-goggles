using ImageGen.Core.Domain.Models;
using ImageGen.Core.Services;

namespace ImageGen.Core.Tests.Services;

public sealed class VideoPriceCalculatorTests
{
    [Fact]
    public void Estimate_VeoAudioAt1080p_UsesModelDefaultAudioRate()
    {
        var model = Model(("duration_seconds_with_audio", 0.4m), ("duration_seconds_without_audio", 0.2m));

        var estimate = VideoPriceCalculator.Estimate(model, 8, "1080p", generateAudio: true);

        Assert.Equal(3.2m, estimate);
    }

    [Fact]
    public void Estimate_VeoAt4k_UsesResolutionSpecificRate()
    {
        var model = Model(("duration_seconds_with_audio", 0.4m), ("duration_seconds_with_audio_4k", 0.6m));

        var estimate = VideoPriceCalculator.Estimate(model, 8, "4K", generateAudio: true);

        Assert.Equal(4.8m, estimate);
    }

    [Fact]
    public void Estimate_KlingWithoutAudio_UsesDurationRate()
    {
        var model = Model(("duration_seconds", 0.084m), ("duration_seconds_with_audio", 0.126m));

        Assert.Equal(0.420m, VideoPriceCalculator.Estimate(model, 5, "720p", generateAudio: false));
    }

    [Fact]
    public void Estimate_RunwayCentsPerSecond_ConvertsToUsd()
    {
        var model = Model(("cents_per_second_output", 12m));

        Assert.Equal(0.60m, VideoPriceCalculator.Estimate(model, 5, "720p", generateAudio: false));
    }

    [Fact]
    public void Estimate_SeedanceVideoTokens_ReturnsUnknown()
    {
        var model = Model(("video_tokens", 0.000007m));

        Assert.Null(VideoPriceCalculator.Estimate(model, 8, "1080p", generateAudio: true));
    }

    [Fact]
    public void Estimate_MinimumCentsPerGeneration_ClampsEstimate()
    {
        var model = Model(("cents_per_second_output", 28m), ("minimum_cents_per_generation", 56m));

        Assert.Equal(0.56m, VideoPriceCalculator.Estimate(model, 1, null, generateAudio: false));
    }

    [Fact]
    public void Estimate_ResolutionSpecificCentsSku_PrecedesGenericSku()
    {
        var model = Model(("cents_per_second_output", 12m), ("cents_per_video_output_second_720p", 20m));

        Assert.Equal(1m, VideoPriceCalculator.Estimate(model, 5, "720p", generateAudio: false));
    }

    [Fact]
    public void Estimate_ImageInputSkuOnly_ReturnsUnknown()
    {
        var model = Model(("cents_per_image_input", 5m));

        Assert.Null(VideoPriceCalculator.Estimate(model, 5, null, generateAudio: false));
    }

    private static VideoModel Model(params (string Key, decimal Value)[] prices) => new()
    {
        ProviderId = "openrouter",
        Id = "vendor/video",
        Name = "Video",
        Durations = [4, 5, 8],
        Resolutions = ["720p", "1080p", "4K"],
        AspectRatios = ["16:9"],
        FrameImages = [],
        GeneratesAudio = true,
        SupportsSeed = false,
        PriceSkus = prices.ToDictionary(price => price.Key, price => price.Value, StringComparer.Ordinal),
        FetchedAt = DateTimeOffset.UnixEpoch,
    };
}
