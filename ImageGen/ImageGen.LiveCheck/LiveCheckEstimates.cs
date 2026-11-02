using ImageGen.Core.Domain.Models;

namespace ImageGen.LiveCheck;

/// <summary>Conservative USD estimates used before each potentially billable image request.</summary>
public static class LiveCheckEstimates
{
    public const decimal UnknownImageCostUsd = 1m;
    private const decimal EstimatedMegapixels = 1m;

    public static decimal FindImageCost(ImageModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var imageCosts = model.Prices
            .Where(price => price.Billable.Equals("output_image", StringComparison.OrdinalIgnoreCase))
            .Select(price => price.Unit switch
            {
                ImagePriceUnit.Image or ImagePriceUnit.Request => price.CostUsd,
                ImagePriceUnit.Megapixel => price.CostUsd * EstimatedMegapixels,
                _ => (decimal?)null,
            })
            .Where(cost => cost.HasValue)
            .Select(cost => cost!.Value)
            .ToArray();

        return imageCosts.Length == 0 ? UnknownImageCostUsd : imageCosts.Max();
    }
}
