using System.Collections.Concurrent;
using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Context;

/// <summary>
/// Per-model correction of the estimate (C2.1): actual / estimated prompt tokens of the last reply, kept in memory in the
/// range 0.5–2.0. Without a reply the factor is 1.0.
/// </summary>
public sealed class TokenCorrection
{
    private const double MinFactor = 0.5;
    private const double MaxFactor = 2.0;

    private readonly ConcurrentDictionary<ModelRef, double> _factors = new();

    public double FactorFor(ModelRef model) => _factors.TryGetValue(model, out var factor) ? factor : 1.0;

    public void Record(ModelRef model, int estimated, int actual)
    {
        if (estimated <= 0 || actual <= 0)
        {
            return;
        }

        _factors[model] = Math.Clamp((double)actual / estimated, MinFactor, MaxFactor);
    }
}
