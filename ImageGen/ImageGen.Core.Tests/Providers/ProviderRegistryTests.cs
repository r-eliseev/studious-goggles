using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public class ProviderRegistryTests
{
    private static ProviderConfig Create(ProviderKind kind, string? responseFormat = null, string? outputFormat = null) => new(
        "provider", kind, "Provider", new Uri("https://provider.test/v1/"), TimeSpan.FromSeconds(60), [], responseFormat, outputFormat);

    [Theory]
    [InlineData("url", null)]
    [InlineData("", null)]
    [InlineData(null, "webp")]
    public void Constructor_InvalidImageFormats_Throws(string? responseFormat, string? outputFormat)
    {
        Assert.Throws<ProviderConfigException>(() => new ProviderRegistry([Create(ProviderKind.OpenAiCompatible, responseFormat, outputFormat)]));
    }

    [Fact]
    public void Constructor_ImageFormatsOnOtherKind_Throws()
    {
        Assert.Throws<ProviderConfigException>(() => new ProviderRegistry([Create(ProviderKind.OpenRouter, "base64")]));
    }
}
