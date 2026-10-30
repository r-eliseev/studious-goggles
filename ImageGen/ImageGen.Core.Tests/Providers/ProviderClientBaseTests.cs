using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public class ProviderClientBaseTests
{
    [Theory]
    [InlineData(400, false, ErrorKind.BadRequest)]
    [InlineData(413, false, ErrorKind.BadRequest)]
    [InlineData(401, false, ErrorKind.InvalidKey)]
    [InlineData(402, false, ErrorKind.InsufficientFunds)]
    [InlineData(403, false, ErrorKind.Forbidden)]
    [InlineData(403, true, ErrorKind.Moderated)]
    [InlineData(404, false, ErrorKind.ModelUnavailable)]
    [InlineData(408, false, ErrorKind.Timeout)]
    [InlineData(502, false, ErrorKind.ModelUnavailable)]
    [InlineData(503, false, ErrorKind.ModelUnavailable)]
    [InlineData(504, false, ErrorKind.Timeout)]
    [InlineData(524, false, ErrorKind.Timeout)]
    [InlineData(529, false, ErrorKind.ModelUnavailable)]
    [InlineData(500, false, ErrorKind.Unknown)]
    [InlineData(422, false, ErrorKind.Unknown)]
    public void MapErrorKind_ReturnsExpectedKind(int status, bool hasModerationReasons, ErrorKind expected)
    {
        Assert.Equal(expected, ProviderClientBase.MapErrorKind(status, hasModerationReasons));
    }
}
