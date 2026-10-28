using ImageGen.Core.Domain.Models;
using ImageGen.Core.Services;

namespace ImageGen.Core.Tests.Services;

public class ReferenceImageLimitTests
{
    [Fact]
    public void Of_InputReferencesRange_ReturnsMax()
    {
        var model = Model([new ModelParameter(ReferenceImageLimit.ParameterName, ParameterKind.Range, [], 0, 4)]);

        Assert.Equal(4, ReferenceImageLimit.Of(model));
    }

    [Fact]
    public void Of_NoInputReferencesParameter_ReturnsZero()
    {
        Assert.Equal(0, ReferenceImageLimit.Of(Model([new ModelParameter("n", ParameterKind.Range, [], 1, 4)])));
    }

    [Fact]
    public void Of_UnknownCatalog_ReturnsZero()
    {
        Assert.Equal(0, ReferenceImageLimit.Of(Model(null)));
        Assert.Equal(0, ReferenceImageLimit.Of(null));
    }

    private static ImageModel Model(IReadOnlyList<ModelParameter>? parameters) => new()
    {
        ProviderId = "openrouter",
        Id = "vendor/a",
        Name = "A",
        SupportsSeed = true,
        SupportsImageInput = false,
        MaxImagesPerRequest = 1,
        FetchedAt = DateTimeOffset.UnixEpoch,
        Parameters = parameters,
    };
}
