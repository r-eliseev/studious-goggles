using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Presentation.Tests.PageModels;

public class ChatItemTemplateChoiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static CopyTextAction Copy() =>
        new(Substitute.For<IClipboardService>(), Substitute.For<IMainThread>(), "text");

    private static MessageItemViewModel Message() => new(
        new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Role = ChatRole.User,
            Text = "hi",
            CreatedAt = Now,
        },
        "hi",
        Copy());

    private static RunItemViewModel Run() => new(new Run
    {
        Id = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Prompt = "fox",
        Origin = RunOrigin.Assistant,
        CreatedAt = Now,
    }, [], Copy());

    [Fact]
    public void Pick_MessageItem_ReturnsMessageTemplate()
    {
        Assert.Equal("message", ChatItemTemplateChoice.Pick<string>(Message(), "message", "run"));
    }

    [Fact]
    public void Pick_SummaryMarker_ReturnsSummaryTemplate()
    {
        var marker = new SummaryMarkerViewModel(
            new ConversationSummary
            {
                SessionId = Guid.NewGuid(),
                Text = "summary",
                CoversUpToMessageId = Guid.NewGuid(),
                Model = new ModelRef(ProviderIds.OpenRouter, "vendor/assistant"),
                CreatedAt = Now,
            },
            Substitute.For<IDialogService>());

        Assert.Equal("summary-template", ChatItemTemplateChoice.Pick<string>(marker, "message", "run", "summary-template"));
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
