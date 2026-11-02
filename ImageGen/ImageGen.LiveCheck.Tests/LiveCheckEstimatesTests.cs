using ImageGen.Core.Domain.Models;
using ImageGen.LiveCheck;

namespace ImageGen.LiveCheck.Tests;

public sealed class LiveCheckEstimatesTests
{
    [Fact]
    public void FindImageCost_IgnoresTokenUnitsAndConservativelyComparesOutputUnits()
    {
        var model = CreateModel(
        [
            new ImageModelPrice("output_image", ImagePriceUnit.Token, 0.000001m, null),
            new ImageModelPrice("output_image", ImagePriceUnit.Image, 0.02m, null),
            new ImageModelPrice("output_image", ImagePriceUnit.Megapixel, 0.015m, null),
            new ImageModelPrice("output_image", ImagePriceUnit.Request, 0.03m, null),
        ]);

        Assert.Equal(0.03m, LiveCheckEstimates.FindImageCost(model));
    }

    [Fact]
    public void FindImageCost_OnlyTokenPriceUsesSafeFallback()
    {
        var model = CreateModel([new ImageModelPrice("output_image", ImagePriceUnit.Token, 0.000001m, null)]);

        Assert.Equal(LiveCheckEstimates.UnknownImageCostUsd, LiveCheckEstimates.FindImageCost(model));
    }

    private static ImageModel CreateModel(IReadOnlyList<ImageModelPrice> prices) => new()
    {
        ProviderId = "openrouter",
        Id = "image-model",
        Name = "Image model",
        SupportsSeed = false,
        SupportsImageInput = false,
        MaxImagesPerRequest = 1,
        FetchedAt = DateTimeOffset.UnixEpoch,
        Prices = prices,
    };
}
