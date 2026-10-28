using ImageGen.Core.Domain.Models;

namespace ImageGen.Presentation.Tests.TestDoubles;

/// <summary>Image model factory for page model tests (the same shape as in the Core tests).</summary>
public static class TestModels
{
    public static ImageModel Model(string id, string name, bool supportsSeed = true) => new()
    {
        Id = id,
        Name = name,
        SupportsSeed = supportsSeed,
        SupportsImageInput = false,
        MaxImagesPerRequest = 1,
        FetchedAt = DateTimeOffset.UnixEpoch,
    };
}
