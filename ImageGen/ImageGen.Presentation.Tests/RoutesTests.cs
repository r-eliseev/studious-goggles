using ImageGen.Presentation;

namespace ImageGen.Presentation.Tests;

public class RoutesTests
{
    [Fact]
    public void ApiKeyFor_ProviderId_EscapesQueryValue()
    {
        Assert.Equal("apikey?providerId=together%20ai%2Fv1", Routes.ApiKeyFor("together ai/v1"));
    }

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

    [Fact]
    public void ChatWithMessage_EscapesTextAndReadsBack()
    {
        var sessionId = Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301");
        const string text = "a fox & a boat, 50% off";

        var route = Routes.ChatWithMessage(sessionId, text);

        Assert.StartsWith("chat?sessionId=3f2504e0-4f89-41d3-9a0c-0305e82c3301&message=", route, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", route, StringComparison.Ordinal);
        var encoded = route[(route.IndexOf("&message=", StringComparison.Ordinal) + "&message=".Length)..];
        Assert.Equal(text, Uri.UnescapeDataString(encoded));
    }
}
