using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Fal;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Fal;

public class FalUrlValidatorTests
{
    private static readonly Uri QueueAddress = new("https://queue.fal.run/");

    [Theory]
    [InlineData("https://queue.fal.run/status", "https://queue.fal.run/status")]
    [InlineData("https://QUEUE.FAL.RUN:443/status", "https://queue.fal.run/status")]
    public void ValidateCallback_AllowsSameOrigin(string value, string expected)
    {
        var result = FalUrlValidator.ValidateCallback(value, QueueAddress);

        Assert.Equal(new Uri(expected), result);
    }

    [Theory]
    [InlineData("https://evil.example/status")]
    [InlineData("http://queue.fal.run/status")]
    [InlineData("https://user@queue.fal.run/status")]
    [InlineData("https://@queue.fal.run/status")]
    [InlineData("https://queue.fal.run/status#fragment")]
    [InlineData("https://queue.fal.run/status#")]
    [InlineData("https:\\evil.example\\status")]
    [InlineData("not a url")]
    public void ValidateCallback_RejectsInvalidOriginWithoutEchoingUrl(string value)
    {
        var exception = Assert.Throws<ProviderException>(() => FalUrlValidator.ValidateCallback(value, QueueAddress));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
        Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateCallback_AllowsLocalHttpWhenBaseIsLocalHttp()
    {
        var baseAddress = new Uri("http://localhost:8123/api/");
        const string value = "http://localhost:8123/status";

        var result = FalUrlValidator.ValidateCallback(value, baseAddress);

        Assert.Equal(new Uri(value), result);
    }

    [Fact]
    public void ValidateCallback_RejectsHttpBaseThatIsNotLocalhost()
    {
        var exception = Assert.Throws<ProviderException>(() =>
            FalUrlValidator.ValidateCallback("http://queue.example/status", new Uri("http://queue.example/")));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
    }

    [Theory]
    [InlineData("fal-ai/flux/dev", "fal-ai/flux/dev")]
    [InlineData("fal-ai/foo%20bar", "fal-ai/foo%20bar")]
    public void ValidateModelEndpoint_AllowsRelativeEndpoint(string value, string expected)
    {
        Assert.Equal(expected, FalUrlValidator.ValidateModelEndpoint(value));
    }

    [Theory]
    [InlineData("https://evil.example/model")]
    [InlineData("//evil.example/model")]
    [InlineData("/fal-ai/model")]
    [InlineData("fal-ai/model?x=1")]
    [InlineData("fal-ai/model#fragment")]
    [InlineData("fal-ai/../secret")]
    [InlineData("fal-ai/%2e%2e/secret")]
    [InlineData("fal-ai/%252e%252e/secret")]
    [InlineData("fal-ai/%2fsecret")]
    [InlineData("fal-ai/%5csecret")]
    [InlineData("fal-ai\\secret")]
    [InlineData("fal-ai/./dev")]
    [InlineData("")]
    public void ValidateModelEndpoint_RejectsInvalidEndpoint(string value)
    {
        var exception = Assert.Throws<ProviderException>(() => FalUrlValidator.ValidateModelEndpoint(value));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
        if (value.Length > 0)
        {
            Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ValidateImage_AllowsPublicHttpsHostname()
    {
        Assert.Equal(new Uri("https://cdn.example/image.png"), FalUrlValidator.ValidateImage("https://cdn.example/image.png"));
    }

    [Theory]
    [InlineData("https://localhost/image")]
    [InlineData("https://localhost./image")]
    [InlineData("https://0.1/image")]
    [InlineData("https://10.1.2.3/image")]
    [InlineData("https://127.0.0.1/image")]
    [InlineData("https://169.254.1.2/image")]
    [InlineData("https://172.16.1.2/image")]
    [InlineData("https://192.168.1.2/image")]
    [InlineData("https://224.0.0.1/image")]
    [InlineData("https://[::]/image")]
    [InlineData("https://[::1]/image")]
    [InlineData("https://[fe80::1]/image")]
    [InlineData("https://[fc00::1]/image")]
    [InlineData("https://[ff02::1]/image")]
    [InlineData("https://[::ffff:127.0.0.1]/image")]
    [InlineData("https://[::ffff:10.0.0.1]/image")]
    [InlineData("https://user@cdn.example/image")]
    [InlineData("https://@cdn.example/image")]
    [InlineData("https://cdn.example/image#fragment")]
    [InlineData("https://cdn.example/image#")]
    [InlineData("https:\\cdn.example\\image")]
    [InlineData("http://cdn.example/image")]
    public void ValidateImage_RejectsNonPublicOrUnsafeAddress(string value)
    {
        var exception = Assert.Throws<ProviderException>(() => FalUrlValidator.ValidateImage(value));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
        Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
    }
}
