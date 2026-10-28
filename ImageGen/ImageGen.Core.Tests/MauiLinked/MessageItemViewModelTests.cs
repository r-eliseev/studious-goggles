using ImageGen.Core.Domain.Chat;
using ImageGen.Maui.PageModels;
using ImageGen.Maui.Resources.Strings;

namespace ImageGen.Core.Tests.MauiLinked;

public class MessageItemViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static ChatMessage Make(ChatRole role, string text = "Hello", bool isError = false) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Role = role,
        Text = text,
        CreatedAt = Now,
        IsError = isError,
    };

    [Fact]
    public void UserMessage_IsUserAndShowsUserRole()
    {
        var item = new MessageItemViewModel(Make(ChatRole.User));

        Assert.True(item.IsUser);
        Assert.Equal(AppResources.UserRole, item.RoleText);
    }

    [Fact]
    public void AssistantMessage_IsNotUserAndShowsAssistantRole()
    {
        var item = new MessageItemViewModel(Make(ChatRole.Assistant));

        Assert.False(item.IsUser);
        Assert.Equal(AppResources.AssistantRole, item.RoleText);
    }

    [Fact]
    public void Constructor_CopiesTextAndId()
    {
        var message = Make(ChatRole.User, text: "draw a red fox");

        var item = new MessageItemViewModel(message);

        Assert.Equal(message.Id, item.Id);
        Assert.Equal("draw a red fox", item.Text);
    }

    [Fact]
    public void ErrorMessage_IsError()
    {
        var item = new MessageItemViewModel(Make(ChatRole.Assistant, isError: true));

        Assert.True(item.IsError);
    }

    [Fact]
    public void Constructor_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MessageItemViewModel(null!));
    }
}
