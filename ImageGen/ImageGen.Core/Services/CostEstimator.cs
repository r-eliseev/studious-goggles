using System.Globalization;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Services;

public sealed class CostEstimator(
    ModelCatalogService catalog,
    VideoModelCatalogService? videoCatalog = null,
    AudioModelCatalogService? audioCatalog = null)
{
    public async Task<CostEstimate> EstimateAudioAsync(
        IReadOnlyList<ModelRef> models,
        string? speechText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(models);
        var known = 0m;
        var unknown = new List<ModelRef>();
        foreach (var reference in models.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            AudioModel? model;
            try
            {
                model = audioCatalog is null ? null : await audioCatalog.FindAsync(reference, cancellationToken).ConfigureAwait(false);
            }
            catch (ProviderException)
            {
                model = null;
            }

            if (model?.Output != AudioOutput.Speech || model.PricePerInputCharacterUsd is null || speechText is null)
            {
                unknown.Add(reference);
                continue;
            }

            known += model.PricePerInputCharacterUsd.Value * speechText.Length;
        }

        return new CostEstimate(known, unknown, IsApproximate: false);
    }

    public async Task<CostEstimate> EstimateVideoAsync(
        IReadOnlyList<ModelRef> models,
        VideoParameters parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parameters.DurationSeconds);

        var known = 0m;
        var unknown = new List<ModelRef>();
        foreach (var reference in models.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            VideoModel? model;
            try
            {
                model = videoCatalog is null ? null : await videoCatalog.FindAsync(reference, cancellationToken);
            }
            catch (ProviderException)
            {
                model = null;
            }

            var price = model is null
                ? null
                : VideoPriceCalculator.Estimate(model, parameters.DurationSeconds, parameters.Resolution, parameters.GenerateAudio);
            if (price is null)
            {
                unknown.Add(reference);
            }
            else
            {
                known += price.Value;
            }
        }

        return new CostEstimate(known, unknown, IsApproximate: false);
    }

    public async Task<CostEstimate> EstimateAsync(
        IReadOnlyList<ModelRef> models,
        int runsPerModel,
        GenerationParameters parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfLessThan(runsPerModel, 1);

        var known = 0m;
        var unknown = new List<ModelRef>();
        var approximate = false;
        foreach (var reference in models.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageModel? model;
            try
            {
                model = await catalog.FindAsync(reference, cancellationToken);
            }
            catch (ProviderException)
            {
                model = null;
            }

            if (model is null || !TryPrice(model, parameters, out var price, out var unit, out var priceIsApproximate))
            {
                unknown.Add(reference);
                continue;
            }

            var unitsPerRun = unit switch
            {
                ImagePriceUnit.Image or ImagePriceUnit.Request => 1m,
                ImagePriceUnit.Megapixel => EstimateMegapixels(parameters),
                _ => null,
            };
            if (unitsPerRun is null)
            {
                unknown.Add(reference);
                continue;
            }

            known += price * unitsPerRun.Value * runsPerModel;
            approximate |= priceIsApproximate || unit == ImagePriceUnit.Megapixel;
        }

        return new CostEstimate(known, unknown, approximate);
    }

    private static bool TryPrice(
        ImageModel model,
        GenerationParameters parameters,
        out decimal cost,
        out ImagePriceUnit unit,
        out bool approximate)
    {
        var prices = model.Prices
            .Where(price => string.Equals(price.Billable, "output_image", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (prices.Count == 0)
        {
            cost = 0;
            unit = default;
            approximate = false;
            return false;
        }

        ImageModelPrice? selected;
        if (!string.IsNullOrWhiteSpace(parameters.Resolution))
        {
            var resolution = NormalizeVariant(parameters.Resolution);
            selected = prices.FirstOrDefault(price => NormalizeVariant(price.Variant) == resolution);
            if (selected is null && prices.All(price => price.Variant is null))
            {
                selected = prices[0];
            }
        }
        else
        {
            selected = prices.Count == 1 ? prices[0] : prices.FirstOrDefault(price => price.Variant is null);
        }

        if (selected is null)
        {
            cost = 0;
            unit = default;
            approximate = false;
            return false;
        }

        cost = selected.CostUsd;
        unit = selected.Unit;
        approximate = false;
        return true;
    }

    private static string NormalizeVariant(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static decimal? EstimateMegapixels(GenerationParameters parameters)
    {
        var resolution = ParseResolution(parameters.Resolution);
        var ratio = ParseAspectRatio(parameters.AspectRatio);
        if (resolution is null || ratio is null)
        {
            return null;
        }

        return (resolution.Value * resolution.Value / 1_000_000m) * ratio.Value;
    }

    private static decimal? ParseResolution(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 1024m;
        }

        var trimmed = value.Trim();
        if (trimmed.EndsWith('K') || trimmed.EndsWith('k'))
        {
            if (!decimal.TryParse(trimmed[..^1], NumberStyles.Number, CultureInfo.InvariantCulture, out var kilo))
            {
                return null;
            }

            return kilo * 1024m;
        }

        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var pixels) && pixels > 0
            ? pixels
            : null;
    }

    private static decimal? ParseAspectRatio(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var width)
            || !decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var height)
            || width <= 0
            || height <= 0)
        {
            return null;
        }

        return Math.Min(width, height) / Math.Max(width, height);
    }
}
