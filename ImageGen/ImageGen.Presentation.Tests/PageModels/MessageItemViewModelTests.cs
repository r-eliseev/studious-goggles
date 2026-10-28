using ImageGen.Core.Domain.Chat;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Presentation.Tests.PageModels;

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

    private static MessageItemViewModel Item(ChatMessage message) =>
        new(message, message.Text, new CopyTextAction(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), message.Text));

    [Fact]
    public void UserMessage_IsUserAndShowsUserRole()
    {
        var item = Item(Make(ChatRole.User));

        Assert.True(item.IsUser);
        Assert.Equal(AppResources.UserRole, item.RoleText);
    }

    [Fact]
    public void AssistantMessage_IsNotUserAndShowsAssistantRole()
    {
        var item = Item(Make(ChatRole.Assistant));

        Assert.False(item.IsUser);
        Assert.Equal(AppResources.AssistantRole, item.RoleText);
    }

    [Fact]
    public void Constructor_CopiesTextAndId()
    {
        var message = Make(ChatRole.User, text: "draw a red fox");

        var item = Item(message);

        Assert.Equal(message.Id, item.Id);
        Assert.Equal("draw a red fox", item.Text);
    }

    [Fact]
    public void ErrorMessage_IsError()
    {
        var item = Item(Make(ChatRole.Assistant, isError: true));

        Assert.True(item.IsError);
    }

    [Fact]
    public void Constructor_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MessageItemViewModel(null!, "text", new CopyTextAction(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), "text")));
    }
}
