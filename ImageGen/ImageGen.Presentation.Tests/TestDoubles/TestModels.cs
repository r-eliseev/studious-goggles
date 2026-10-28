using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;

namespace ImageGen.Presentation.Tests.TestDoubles;

/// <summary>Image model factory for page model tests (the same shape as in the Core tests).</summary>
public static class TestModels
{
    public static ImageModel Model(string id, string name, bool supportsSeed = true) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = name,
        SupportsSeed = supportsSeed,
        SupportsImageInput = false,
        MaxImagesPerRequest = 1,
        FetchedAt = DateTimeOffset.UnixEpoch,
        Parameters = [],
    };
}
