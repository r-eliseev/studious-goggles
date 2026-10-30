using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public sealed class InMemoryQueuedImageConnectionTests
{
    [Fact]
    public async Task Connection_ExposesQueuedImageCapabilityAndDelegatesCatalogChatAndTransport()
    {
        var images = Substitute.For<IModelProvider>();
        var chat = Substitute.For<IAssistantChatClient>();
        var transport = Substitute.For<ISynchronousImageTransport>();
        var models = Array.Empty<ImageModel>();
        var chatModels = new[] { new ChatModel("assistant", "Assistant", false, null, true) };
        var chatResult = new AssistantChatResult("hello", [], 0.01m);
        var generationResult = new ImageGenerationResult([1, 2, 3], "image/png", 0.02m);
        images.CheckKeyAsync("key", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Valid);
        images.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns(models);
        chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(chatModels);
        chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>()).Returns(chatResult);
        transport.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(generationResult);
        var connection = new InMemoryQueuedImageConnection(images, chat, transport, "local", TimeProvider.System);
        var queued = (IQueuedImageProvider)connection;
        var request = Request();
        var chatRequest = new AssistantChatRequest("assistant", [], []);

        Assert.Equal(ApiKeyStatus.Valid, await connection.CheckKeyAsync("key", TestContext.Current.CancellationToken));
        Assert.Same(models, await connection.ListImageModelsAsync(TestContext.Current.CancellationToken));
        Assert.Same(chatModels, await connection.ListChatModelsAsync(TestContext.Current.CancellationToken));
        Assert.Same(chatResult, await connection.CompleteChatAsync(chatRequest, TestContext.Current.CancellationToken));
        var handle = await queued.SubmitAsync(request, TestContext.Current.CancellationToken);
        var terminal = await WaitForTerminalAsync(queued, handle, TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Succeeded, terminal.State);
        Assert.Same(generationResult, terminal.Result);
        await transport.Received(1).GenerateAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueuedJobs_CanBeCancelledAndHandlesAreIsolatedPerProvider()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = Substitute.For<ISynchronousImageTransport>();
        transport.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
            return new ImageGenerationResult([], "image/png", null);
        });
        var first = NewConnection(transport, "local-a");
        var second = NewConnection(transport, "local-b");
        var cancellationToken = TestContext.Current.CancellationToken;
        var handle = await first.SubmitAsync(Request(), cancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        Assert.Equal(JobState.Running, (await first.PollAsync(handle, cancellationToken)).State);
        Assert.Equal(CancelOutcome.CancellationRequested, await first.CancelAsync(handle, cancellationToken));
        Assert.Equal(JobState.Cancelled, (await WaitForTerminalAsync(first, handle, cancellationToken)).State);
        var exception = await Assert.ThrowsAsync<ProviderException>(() => second.PollAsync(handle, cancellationToken));
        Assert.Equal(ErrorKind.Unknown, exception.Kind);
    }

    private static InMemoryQueuedImageConnection NewConnection(ISynchronousImageTransport transport, string providerId) =>
        new(Substitute.For<IModelProvider>(), Substitute.For<IAssistantChatClient>(), transport, providerId, TimeProvider.System);

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
