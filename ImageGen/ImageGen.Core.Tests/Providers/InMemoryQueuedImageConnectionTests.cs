using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public sealed class InMemoryQueuedImageConnectionTests
{
    private const string ProviderId = "local";

    [Fact]
    public async Task CurrentInterfaces_DelegateToInnerProvidersAndGenerateRemainsDirect()
    {
        var image = Substitute.For<IModelProvider>();
        var chat = Substitute.For<IAssistantChatClient>();
        var models = Array.Empty<ImageModel>();
        var chatModels = new[] { new ChatModel("assistant", "Assistant", false, null, true) };
        var chatResult = new AssistantChatResult("hello", [], 0.01m);
        var generationResult = new ImageGenerationResult([1, 2, 3], "image/png", 0.02m);
        image.CheckKeyAsync("key", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Valid);
        image.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns(models);
        image.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(generationResult);
        chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(chatModels);
        chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>()).Returns(chatResult);
        var connection = new InMemoryQueuedImageConnection(image, chat, ProviderId, TimeProvider.System);
        var request = Request();
        var chatRequest = new AssistantChatRequest("assistant", [], []);

        Assert.Equal(ApiKeyStatus.Valid, await connection.CheckKeyAsync("key", TestContext.Current.CancellationToken));
        Assert.Same(models, await connection.ListImageModelsAsync(TestContext.Current.CancellationToken));
        Assert.Same(chatModels, await connection.ListChatModelsAsync(TestContext.Current.CancellationToken));
        Assert.Same(chatResult, await connection.CompleteChatAsync(chatRequest, TestContext.Current.CancellationToken));
        Assert.Same(generationResult, await connection.GenerateAsync(request, TestContext.Current.CancellationToken));
        Assert.False((IProviderConnection)connection is IQueuedImageProvider);
        Assert.Same(connection.QueuedImages, connection.QueuedImages);
        await image.Received(1).GenerateAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueuedImages_SubmitPollCancel_ShareOneAdapterJobDictionary()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var image = Substitute.For<IModelProvider>();
        image.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
            return new ImageGenerationResult([], "image/png", null);
        });
        var connection = new InMemoryQueuedImageConnection(image, Substitute.For<IAssistantChatClient>(), ProviderId, TimeProvider.System);
        var cancellationToken = TestContext.Current.CancellationToken;

        var handle = await connection.QueuedImages.SubmitAsync(Request(), cancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        Assert.Equal(ProviderId, handle.ProviderId);
        Assert.Equal(JobState.Running, (await connection.QueuedImages.PollAsync(handle, cancellationToken)).State);
        Assert.Equal(CancelOutcome.CancellationRequested, await connection.QueuedImages.CancelAsync(handle, cancellationToken));

        var terminal = await WaitForTerminalAsync(connection.QueuedImages, handle, cancellationToken);

        Assert.Equal(JobState.Cancelled, terminal.State);
    }

    [Fact]
    public async Task QueuedImages_HandleFromAnotherProvider_IsRejected()
    {
        var image = Substitute.For<IModelProvider>();
        image.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ImageGenerationResult([], "image/png", null)));
        var first = new InMemoryQueuedImageConnection(image, Substitute.For<IAssistantChatClient>(), "local-a", TimeProvider.System);
        var second = new InMemoryQueuedImageConnection(image, Substitute.For<IAssistantChatClient>(), "local-b", TimeProvider.System);
        var cancellationToken = TestContext.Current.CancellationToken;
        var handle = await first.QueuedImages.SubmitAsync(Request(), cancellationToken);
        await WaitForTerminalAsync(first.QueuedImages, handle, cancellationToken);

        var exception = await Assert.ThrowsAsync<ProviderException>(() => second.QueuedImages.PollAsync(handle, cancellationToken));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
    }

    private static ImageGenerationRequest Request() => new("model", "prompt", null, null, null, null, null, []);

    private static async Task<JobUpdate> WaitForTerminalAsync(
        IQueuedImageProvider provider,
        ProviderJobHandle handle,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var update = await provider.PollAsync(handle, cancellationToken);
            if (update.IsTerminal)
            {
                return update;
            }

            await Task.Delay(10, cancellationToken);
        }

        throw new TimeoutException("The in-memory generation did not finish in time.");
    }
}
