using System.Net;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Core.Services;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Flows;

public class AssistantRetryFlowTests
{
    [AvaloniaFact]
    public async Task TryAgain_AfterErrorReply_AnswersAgainWithoutNewUserMessage()
    {
        using var shell = new ShellHarness(
            openRouter: OpenRouterFakes.Create(chatStatus: HttpStatusCode.TooManyRequests, failedChatReplies: 1));
        var chat = await shell.OpenNewChatAsync();
        chat.InputText = "draw a red fox";
        await chat.SendCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => chat.Items[^1] is MessageItemViewModel { CanRetry: true }, "«Try again» после ошибки");

        await ((MessageItemViewModel)chat.Items[^1]).RetryCommand.ExecuteAsync(null);
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();

        await shell.WaitUntilAsync(() => chat.Items.OfType<RunItemViewModel>().Any(), "ответ после повтора");
        Assert.Single(chat.Items.OfType<MessageItemViewModel>(), message => message.IsUser);
        Assert.DoesNotContain(chat.Items.OfType<MessageItemViewModel>(), message => message is { IsError: true, CanRetry: true });
    }
}
