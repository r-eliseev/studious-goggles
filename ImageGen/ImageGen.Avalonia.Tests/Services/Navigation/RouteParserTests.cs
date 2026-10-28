using ImageGen.Avalonia.Services.Navigation;

namespace ImageGen.Avalonia.Tests.Services.Navigation;

public class RouteParserTests
{
    [Fact]
    public void Parse_PathOnly_HasNoQuery()
    {
        var (path, query) = RouteParser.Parse("apikey");

        Assert.Equal("apikey", path);
        Assert.Empty(query);
    }

    [Fact]
    public void Parse_SessionIdQuery_ReturnsValue()
    {
        var (path, query) = RouteParser.Parse("chat?sessionId=3f2504e0-4f89-41d3-9a0c-0305e82c3301");

        Assert.Equal("chat", path);
        Assert.Equal("3f2504e0-4f89-41d3-9a0c-0305e82c3301", query["sessionId"]);
    }
}
