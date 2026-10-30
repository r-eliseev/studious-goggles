using System.Globalization;
using ImageGen.Core.Domain.Models;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

/// <summary>Formats provider image prices for the model catalog without relying on the machine culture.</summary>
public static class ImagePriceText
{
    private static readonly ImagePriceUnit[] UnitPriority =
    [
        ImagePriceUnit.Image,
        ImagePriceUnit.Megapixel,
        ImagePriceUnit.Token,
        ImagePriceUnit.Request,
    ];

    public static string Format(IReadOnlyList<ImageModelPrice> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);
        var outputPrices = prices.Where(price => string.Equals(price.Billable, "output_image", StringComparison.Ordinal)).ToList();
        foreach (var unit in UnitPriority)
        {
            var unitPrices = outputPrices.Where(price => price.Unit == unit).Select(price =>
                unit == ImagePriceUnit.Token ? price.CostUsd * 1_000_000m : price.CostUsd).ToList();
            if (unitPrices.Count == 0)
            {
                continue;
            }

            var minimum = unitPrices.Min();
            var maximum = unitPrices.Max();
            var amount = minimum == maximum
                ? FormatNumber(minimum)
                : FormatNumber(minimum) + "–" + FormatNumber(maximum);
            var format = unit switch
            {
                ImagePriceUnit.Image => AppResources.ImagePricePerImageFormat,
                ImagePriceUnit.Megapixel => AppResources.ImagePricePerMegapixelFormat,
                ImagePriceUnit.Token => AppResources.ImagePricePerMillionTokensFormat,
                ImagePriceUnit.Request => AppResources.ImagePricePerRequestFormat,
                _ => throw new ArgumentOutOfRangeException(nameof(prices)),
            };
            return string.Format(CultureInfo.InvariantCulture, format, amount);
        }

        return AppResources.AssistantPriceUnknown;
    }

    private static string FormatNumber(decimal value) => value.ToString("0.00##", CultureInfo.InvariantCulture);
}
