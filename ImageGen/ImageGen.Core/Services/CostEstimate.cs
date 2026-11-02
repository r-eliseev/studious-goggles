using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Services;

public sealed record CostEstimate(decimal KnownUsd, IReadOnlyList<ModelRef> UnknownModels, bool IsApproximate);
