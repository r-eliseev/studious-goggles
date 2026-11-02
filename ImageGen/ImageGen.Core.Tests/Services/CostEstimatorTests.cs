using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public sealed class CostEstimatorTests
{
    [Fact]
    public async Task EstimateAsync_ImageAndRequestPrices_MultipliesByRunsAndModels()
    {
        var image = Model("image", [new ImageModelPrice("output_image", ImagePriceUnit.Image, 0.02m, null)]);
        var request = Model("request", [new ImageModelPrice("output_image", ImagePriceUnit.Request, 0.10m, null)]);
        var estimator = CreateEstimator(image, request);

        var estimate = await estimator.EstimateAsync([Ref("image"), Ref("request")], 3, GenerationParameters.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(0.36m, estimate.KnownUsd);
        Assert.Empty(estimate.UnknownModels);
        Assert.False(estimate.IsApproximate);
    }

    [Fact]
    public async Task EstimateAsync_MegapixelPrice_UsesAspectRatioAndMarksApproximate()
    {
        var model = Model("mp", [new ImageModelPrice("output_image", ImagePriceUnit.Megapixel, 0.01m, null)]);
        var estimator = CreateEstimator(model);
        var parameters = new GenerationParameters { AspectRatio = "16:9", Resolution = "1K" };

        var estimate = await estimator.EstimateAsync([Ref("mp")], 2, parameters, TestContext.Current.CancellationToken);

        Assert.Equal(0.01179648m, estimate.KnownUsd);
        Assert.True(estimate.IsApproximate);
    }

    [Fact]
    public async Task EstimateAsync_TokenAndMissingPrices_AreReportedUnknown()
    {
        var token = Model("token", [new ImageModelPrice("output_image", ImagePriceUnit.Token, 0.00001m, null)]);
        var noPrice = Model("no-price", []);
        var estimator = CreateEstimator(token, noPrice);

        var estimate = await estimator.EstimateAsync([Ref("token"), Ref("no-price"), Ref("missing")], 1, GenerationParameters.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(0m, estimate.KnownUsd);
        Assert.Equal([Ref("token"), Ref("no-price"), Ref("missing")], estimate.UnknownModels);
    }

    [Fact]
    public async Task EstimateAsync_ResolutionVariant_SelectsMatchingCatalogPrice()
    {
        var model = Model("variants",
        [
            new ImageModelPrice("output_image", ImagePriceUnit.Image, 0.01m, "1k"),
            new ImageModelPrice("output_image", ImagePriceUnit.Image, 0.03m, "2k"),
        ]);
        var estimator = CreateEstimator(model);

        var estimate = await estimator.EstimateAsync(
            [Ref("variants")], 2, new GenerationParameters { Resolution = "2K" }, TestContext.Current.CancellationToken);

        Assert.Equal(0.06m, estimate.KnownUsd);
        Assert.Empty(estimate.UnknownModels);
        Assert.False(estimate.IsApproximate);
    }

    private static CostEstimator CreateEstimator(params ImageModel[] models)
    {
        var providers = Substitute.For<IProviderRouter>();
        providers.ImageProviderIds.Returns([ProviderIds.OpenRouter]);
        var repository = Substitute.For<IImageModelRepository>();
        repository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ImageModel>>(models));
        var catalog = new ModelCatalogService(providers, repository, NullLogger<ModelCatalogService>.Instance);
        return new CostEstimator(catalog);
    }

    private static ModelRef Ref(string id) => new(ProviderIds.OpenRouter, id);

    private static ImageModel Model(string id, IReadOnlyList<ImageModelPrice> prices) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = id,
        SupportsSeed = true,
        SupportsImageInput = false,
        MaxImagesPerRequest = 1,
        FetchedAt = DateTimeOffset.UnixEpoch,
        Parameters = [],
        Prices = prices,
    };
}
