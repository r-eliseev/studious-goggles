using System.Globalization;
using ImageGen.Core.Domain.Models;
using ImageGen.Presentation.PageModels;

namespace ImageGen.Presentation.Tests.PageModels;

public sealed class ImagePriceTextTests
{
    [Fact]
    public void Format_OnePerImage_ShowsPrice()
    {
        Assert.Equal("$0.04 / image", ImagePriceText.Format([Price("output_image", ImagePriceUnit.Image, 0.04m)]));
    }

    [Fact]
    public void Format_Variants_ShowsRange()
    {
        Assert.Equal("$0.008–0.036 / image", ImagePriceText.Format(
        [
            Price("output_image", ImagePriceUnit.Image, 0.008m, "768"),
            Price("output_image", ImagePriceUnit.Image, 0.009m, "1k"),
            Price("output_image", ImagePriceUnit.Image, 0.011m, "1.5k"),
            Price("output_image", ImagePriceUnit.Image, 0.014m, "2k"),
            Price("output_image", ImagePriceUnit.Image, 0.036m, "4k"),
        ]));
    }

    [Fact]
    public void Format_EqualVariants_ShowsOneNumber()
    {
        Assert.Equal("$0.0184 / image", ImagePriceText.Format(
        [
            Price("output_image", ImagePriceUnit.Image, 0.0184m, "1k"),
            Price("output_image", ImagePriceUnit.Image, 0.0184m, "2k"),
        ]));
    }

    [Fact]
    public void Format_Megapixel_ShowsPerMp()
    {
        Assert.Equal("$0.014 / MP", ImagePriceText.Format([Price("output_image", ImagePriceUnit.Megapixel, 0.014m)]));
    }

    [Fact]
    public void Format_Token_ShowsPerMillion()
    {
        Assert.Equal("$40.00 / 1M output tokens", ImagePriceText.Format([Price("output_image", ImagePriceUnit.Token, 0.00004m)]));
    }

    [Fact]
    public void Format_ImageAndRequest_PrefersImage()
    {
        Assert.Equal("$0.03–0.06 / image", ImagePriceText.Format(
        [
            Price("output_image", ImagePriceUnit.Image, 0.03m),
            Price("output_image", ImagePriceUnit.Image, 0.06m, "2k"),
            Price("output_image", ImagePriceUnit.Request, 0.02m, "high_thinking_surcharge"),
        ]));
    }

    [Fact]
    public void Format_OnlyInputs_Unknown()
    {
        Assert.Equal("price unknown", ImagePriceText.Format([Price("input_image", ImagePriceUnit.Image, 0.02m)]));
    }

    [Fact]
    public void Format_Empty_Unknown() => Assert.Equal("price unknown", ImagePriceText.Format([]));

    [Fact]
    public void Format_PolishCulture_UsesPoint()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

            Assert.Equal("$0.014 / MP", ImagePriceText.Format([Price("output_image", ImagePriceUnit.Megapixel, 0.014m)]));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static ImageModelPrice Price(string billable, ImagePriceUnit unit, decimal cost, string? variant = null) =>
        new(billable, unit, cost, variant);
}
