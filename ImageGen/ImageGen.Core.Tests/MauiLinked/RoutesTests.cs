using ImageGen.Maui;

namespace ImageGen.Core.Tests.MauiLinked;

public class RoutesTests
{
    [Fact]
    public void ChatFor_SessionId_ReturnsChatRouteWithSessionIdQuery()
    {
        var sessionId = Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301");

        Assert.Equal("chat?sessionId=3f2504e0-4f89-41d3-9a0c-0305e82c3301", Routes.ChatFor(sessionId));
    }

    [Fact]
    public void SessionModelsFor_SessionId_ReturnsModelsRouteWithSessionIdQuery()
    {
        var sessionId = Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301");

        Assert.Equal("models?sessionId=3f2504e0-4f89-41d3-9a0c-0305e82c3301", Routes.SessionModelsFor(sessionId));
    }

    [Fact]
    public void ChatFor_UpperCaseGuid_UsesLowerCaseDashedFormat()
    {
        var sessionId = Guid.Parse("3F2504E0-4F89-41D3-9A0C-0305E82C3301");

        Assert.Equal("chat?sessionId=3f2504e0-4f89-41d3-9a0c-0305e82c3301", Routes.ChatFor(sessionId));
    }
}
