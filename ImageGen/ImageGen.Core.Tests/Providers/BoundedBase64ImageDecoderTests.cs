using System.Text;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public sealed class BoundedBase64ImageDecoderTests
{
    [Theory]
    [InlineData("AQID", 3)]
    [InlineData(" \tAQ\r\nID\n", 3)]
    [InlineData("AQ==", 1)]
    [InlineData(" A Q = = ", 1)]
    [InlineData("AQI=", 2)]
    [InlineData("A Q I =", 2)]
    public void Decode_AllowedWhitespaceAndPadding_ReturnsBytes(string input, long limit)
    {
        var result = BoundedBase64ImageDecoder.Decode(input, limit);

        Assert.Equal(Convert.FromBase64String(input), result);
    }

    [Fact]
    public void Decode_EncodedLengthOverLimit_ThrowsBeforeDecoding()
    {
        Assert.Throws<ProviderResponseTooLargeException>(() => BoundedBase64ImageDecoder.Decode("AQIDBA==", 3));
    }

    [Theory]
    [InlineData("%%%=")]
    [InlineData("AQ\u00a0ID")]
    public void Decode_InvalidCharacters_KeepFormatException(string input)
    {
        Assert.Throws<FormatException>(() => BoundedBase64ImageDecoder.Decode(input, 10));
    }

    [Fact]
    public void Decode_ExactProductionLimit_WithAsciiWhitespace_IsAccepted()
    {
        var content = new byte[ProviderResponseLimits.Image];
        var encoded = Convert.ToBase64String(content);
        var wrappedBuilder = new StringBuilder(encoded.Length + encoded.Length / 76 * 2);
        for (var offset = 0; offset < encoded.Length; offset += 76)
        {
            if (offset > 0) wrappedBuilder.Append("\r\n");
            wrappedBuilder.Append(encoded, offset, Math.Min(76, encoded.Length - offset));
        }
        var wrapped = wrappedBuilder.ToString();

        var result = BoundedBase64ImageDecoder.Decode(wrapped, ProviderResponseLimits.Image);

        Assert.Equal(ProviderResponseLimits.Image, result.LongLength);
    }
}
