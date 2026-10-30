namespace ImageGen.Core.Domain.Models;

/// <summary>A price for one billable output/input unit reported by a provider's image model catalog.</summary>
/// <param name="Billable">Provider-defined billable item such as <c>output_image</c>.</param>
/// <param name="Unit">Provider-defined unit used to price the billable item.</param>
/// <param name="CostUsd">Cost for one provider unit; for <see cref="ImagePriceUnit.Token"/> this is one token.</param>
/// <param name="Variant">Optional provider variant such as a resolution tier.</param>
public sealed record ImageModelPrice(string Billable, ImagePriceUnit Unit, decimal CostUsd, string? Variant);
