using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Maui.PageModels;

namespace ImageGen.Core.Tests.MauiLinked;

public class ChatItemTemplateChoiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static MessageItemViewModel Message() => new(new ChatMessage
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Role = ChatRole.User,
        Text = "hi",
        CreatedAt = Now,
    });

    private static RunItemViewModel Run() => new(new Run
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Prompt = "fox",
        Origin = RunOrigin.Assistant,
        CreatedAt = Now,
    }, []);

    [Fact]
    public void Pick_MessageItem_ReturnsMessageTemplate()
    {
        Assert.Equal("message", ChatItemTemplateChoice.Pick<string>(Message(), "message", "run"));
    }

    [Fact]
    public void Pick_RunItem_ReturnsRunTemplate()
    {
        Assert.Equal("run", ChatItemTemplateChoice.Pick<string>(Run(), "message", "run"));
    }

    [Fact]
    public void Pick_MessageItemWithoutMessageTemplate_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ChatItemTemplateChoice.Pick<string>(Message(), null, "run"));
    }

    [Fact]
    public void Pick_RunItemWithoutRunTemplate_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ChatItemTemplateChoice.Pick<string>(Run(), "message", null));
    }

    [Fact]
    public void Pick_NullItem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ChatItemTemplateChoice.Pick<string>(null!, "message", "run"));
    }
}
