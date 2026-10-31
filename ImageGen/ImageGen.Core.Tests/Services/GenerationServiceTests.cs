using System.Collections.Concurrent;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Core.Tests.Diagnostics;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public class GenerationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private const string Seeded = "vendor/seeded";
    private const string Unseeded = "vendor/unseeded";
    private static readonly byte[] Image = [1, 2, 3];

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly ISynchronousImageTransport _generator = Substitute.For<ISynchronousImageTransport>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly ISeedSource _seeds = Substitute.For<ISeedSource>();
    private readonly IImageModelRepository _modelRepository = Substitute.For<IImageModelRepository>();
    private readonly ConcurrentQueue<Generation> _events = new();
    private readonly CapturingLogger<GenerationService> _logger = new();
    private readonly RecordingAsyncDelay _delay = new();
    private readonly IChatAttachmentRepository _chatAttachments = Substitute.For<IChatAttachmentRepository>();
    private readonly AttachmentService _attachments;
    private readonly GenerationService _service;
    private readonly SessionChangeNotifier _changes = new(NullLogger<SessionChangeNotifier>.Instance);
    private readonly Session _session;
    private IReadOnlyList<Generation> _added = [];
    private readonly ConcurrentDictionary<Guid, Run> _savedRuns = new();

    [Fact]
    public async Task StopSessionAsync_TombstoneRejectsLaterRunBeforePersistence()
    {
        await _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken));

        await _runs.DidNotReceive().AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>());
        _providers.DidNotReceive().GetImageProvider(Arg.Any<string>());
    }

    [Theory]
    [InlineData(CancelOutcome.Cancelled)]
    [InlineData(CancelOutcome.CancellationRequested)]
    [InlineData(CancelOutcome.AlreadyCompleted)]
    [InlineData(CancelOutcome.NotFound)]
    public async Task StopSessionAsync_SubmitReturnsHandleAfterCancellation_RemotelyCancelsBeforeStopCompletes(CancelOutcome cancelOutcome)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubmit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = Handle();
        var connection = Substitute.For<IProviderConnection, IModelProvider, IQueuedImageProvider>();
        var imageProvider = (IModelProvider)connection;
        var queued = (IQueuedImageProvider)connection;
        _session.Settings.ImageModels = [new ModelRef("fal", "flux")];
        _session.Settings.RunsPerModel = 1;
        _modelRepository.ListAsync("fal", Arg.Any<CancellationToken>()).Returns([ModelCatalogServiceTests.Model("flux", "Flux", providerId: "fal")]);
        _providers.GetImageProvider("fal").Returns(imageProvider);
        _providers.GetQueuedImageProvider("fal").Returns(queued);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            entered.TrySetResult();
            await releaseSubmit.Task;
            return handle;
        });
        queued.CancelAsync(handle, Arg.Any<CancellationToken>()).Returns(cancelOutcome);
        queued.ClearReceivedCalls();

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var stop = _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);
        releaseSubmit.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await queued.Received(1).CancelAsync(handle, Arg.Is<CancellationToken>(token => !token.IsCancellationRequested));
        await queued.DidNotReceive().PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopSessionAsync_RemoteCancelFailure_DoesNotBlockLocalCleanup()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSubmit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = Handle();
        var connection = Substitute.For<IProviderConnection, IModelProvider, IQueuedImageProvider>();
        var imageProvider = (IModelProvider)connection;
        var queued = (IQueuedImageProvider)connection;
        _session.Settings.ImageModels = [new ModelRef("fal", "flux")];
        _session.Settings.RunsPerModel = 1;
        _modelRepository.ListAsync("fal", Arg.Any<CancellationToken>()).Returns([ModelCatalogServiceTests.Model("flux", "Flux", providerId: "fal")]);
        _providers.GetImageProvider("fal").Returns(imageProvider);
        _providers.GetQueuedImageProvider("fal").Returns((IQueuedImageProvider)connection);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            entered.TrySetResult();
            await releaseSubmit.Task;
            return handle;
        });
        queued.CancelAsync(handle, Arg.Any<CancellationToken>()).Returns<Task<CancelOutcome>>(_ =>
            throw new ProviderException(ErrorKind.Network, "sensitive provider detail"));
        queued.ClearReceivedCalls();

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var stop = _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);
        releaseSubmit.TrySetResult();

        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await queued.Received(1).CancelAsync(handle, Arg.Any<CancellationToken>());
        var warning = Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Null(warning.Exception);
        Assert.DoesNotContain("sensitive provider detail", warning.Message, StringComparison.Ordinal);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Exception is ProviderException);
    }

    [Fact]
    public async Task StartRunAsync_FourJobsForOneProvider_DoNotBlockAnotherProvider()
    {
        var otherSession = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Other",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = new SessionSettings
            {
                ImageModels = [new ModelRef("fal", "flux")],
                RunsPerModel = 1,
                AssistantModel = new ModelRef(ProviderIds.OpenRouter, SessionSettings.DefaultAssistantModelId),
            },
        };
        _sessions.GetAsync(otherSession.Id, Arg.Any<CancellationToken>()).Returns(otherSession);
        _modelRepository.ListAsync("fal", Arg.Any<CancellationToken>()).Returns([ModelCatalogServiceTests.Model("flux", "Flux", providerId: "fal")]);
        var providerB = Substitute.For<ISynchronousImageTransport>();
        _providers.GetImageProvider("fal").Returns(Substitute.For<IModelProvider>());
        _providers.GetQueuedImageProvider("fal").Returns(new InMemoryQueuedImageProvider(providerB, "fal", TimeProvider.System));
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 4;
        var providerAEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerBEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseB = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerACount = 0;
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            if (Interlocked.Increment(ref providerACount) == 4)
            {
                providerAEntered.TrySetResult();
            }

            return await releaseA.Task;
        });
        providerB.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            providerBEntered.TrySetResult();
            return await releaseB.Task;
        });

        await _service.StartRunAsync(_session.Id, "first", TestContext.Current.CancellationToken);
        await providerAEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _service.StartRunAsync(otherSession.Id, "second", TestContext.Current.CancellationToken);
        await providerBEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        releaseA.TrySetResult(new ImageGenerationResult(Image, "image/png", null));
        releaseB.TrySetResult(new ImageGenerationResult(Image, "image/png", null));
        await _service.WhenIdleAsync();
    }

    [Fact]
    public async Task StartRunAsync_QueuedJobs_HoldProviderSlotsUntilTerminal()
    {
        _session.Settings.ImageModels = [new ModelRef("fal", "flux")];
        _session.Settings.RunsPerModel = 5;
        _modelRepository.ListAsync("fal", Arg.Any<CancellationToken>()).Returns([ModelCatalogServiceTests.Model("flux", "Flux", providerId: "fal")]);
        var connection = Substitute.For<IProviderConnection, IModelProvider, IQueuedImageProvider>();
        var queued = (IQueuedImageProvider)connection;
        _providers.GetImageProvider("fal").Returns((IModelProvider)connection);
        _providers.GetQueuedImageProvider("fal").Returns((IQueuedImageProvider)connection);
        var submitCount = 0;
        var pollCount = 0;
        var fourPollsEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fifthSubmitEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishFirst = new TaskCompletionSource<JobUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var index = Interlocked.Increment(ref submitCount);
            if (index == 5)
            {
                fifthSubmitEntered.TrySetResult();
            }

            return Task.FromResult(new ProviderJobHandle("fal", $"req-{index}", $"https://q/{index}/s", $"https://q/{index}/r", $"https://q/{index}/c"));
        });
        queued.PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var handle = call.Arg<ProviderJobHandle>();
            if (Interlocked.Increment(ref pollCount) == 4)
            {
                fourPollsEntered.TrySetResult();
            }

            return handle.RequestId == "req-1" ? finishFirst.Task : Task.FromResult(new JobUpdate(JobState.Queued));
        });
        queued.CancelAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>()).Returns(CancelOutcome.Cancelled);
        queued.ClearReceivedCalls();

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await fourPollsEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(4, Volatile.Read(ref submitCount));
        Assert.False(fifthSubmitEntered.Task.IsCompleted);

        finishFirst.TrySetResult(new JobUpdate(JobState.Failed, ErrorKind: ErrorKind.Unknown));
        await fifthSubmitEntered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);
        Assert.Equal(5, Volatile.Read(ref submitCount));
    }

    [Fact]
    public async Task StopSessionAsync_QueuedJobWaitingForProviderSlot_DoesNotSubmitOrReleaseUnacquiredSlot()
    {
        _session.Settings.ImageModels = [new ModelRef("fal", "flux")];
        _session.Settings.RunsPerModel = 5;
        _modelRepository.ListAsync("fal", Arg.Any<CancellationToken>()).Returns([ModelCatalogServiceTests.Model("flux", "Flux", providerId: "fal")]);
        var connection = Substitute.For<IProviderConnection, IModelProvider, IQueuedImageProvider>();
        var queued = (IQueuedImageProvider)connection;
        _providers.GetImageProvider("fal").Returns((IModelProvider)connection);
        _providers.GetQueuedImageProvider("fal").Returns((IQueuedImageProvider)connection);
        var submitCount = 0;
        var pollCount = 0;
        var fourPollsEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var index = Interlocked.Increment(ref submitCount);
            return Task.FromResult(new ProviderJobHandle("fal", $"req-{index}", $"https://q/{index}/s", $"https://q/{index}/r", $"https://q/{index}/c"));
        });
        queued.PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (Interlocked.Increment(ref pollCount) == 4)
            {
                fourPollsEntered.TrySetResult();
            }

            return Task.FromResult(new JobUpdate(JobState.Queued));
        });
        queued.CancelAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>()).Returns(CancelOutcome.Cancelled);
        queued.ClearReceivedCalls();

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await fourPollsEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(4, Volatile.Read(ref submitCount));

        await _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);

        Assert.Equal(4, Volatile.Read(ref submitCount));
        await queued.Received(4).SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
        await queued.Received(4).CancelAsync(Arg.Any<ProviderJobHandle>(), CancellationToken.None);
    }

    [Fact]
    public async Task RecoverInterruptedAsync_FourWatchers_HoldSlotsAgainstFreshSubmit()
    {
        _session.Settings.ImageModels = [new ModelRef("fal", "flux")];
        _session.Settings.RunsPerModel = 1;
        _modelRepository.ListAsync("fal", Arg.Any<CancellationToken>()).Returns([ModelCatalogServiceTests.Model("flux", "Flux", providerId: "fal")]);
        var generations = Enumerable.Range(0, 4).Select(_ => ResumableGeneration()).ToArray();
        var handle = Handle();
        foreach (var generation in generations)
        {
            generation.JobJson = System.Text.Json.JsonSerializer.Serialize(handle);
        }

        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns(generations);
        var connection = Substitute.For<IProviderConnection, IModelProvider, IQueuedImageProvider>();
        var queued = (IQueuedImageProvider)connection;
        _providers.GetQueuedImageProvider("fal").Returns((IQueuedImageProvider)connection);
        _providers.GetImageProvider("fal").Returns((IModelProvider)connection);
        var pollCount = 0;
        var fourPollsEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWatchers = new TaskCompletionSource<JobUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        var freshSubmitEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        queued.PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (Interlocked.Increment(ref pollCount) == 4)
            {
                fourPollsEntered.TrySetResult();
            }

            return await releaseWatchers.Task.WaitAsync(call.Arg<CancellationToken>());
        });
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            freshSubmitEntered.TrySetResult();
            return Task.FromResult(handle);
        });
        queued.ClearReceivedCalls();

        await _service.RecoverInterruptedAsync(TestContext.Current.CancellationToken);
        await fourPollsEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _service.StartRunAsync(_session.Id, "fresh", TestContext.Current.CancellationToken);
        Assert.False(freshSubmitEntered.Task.IsCompleted);

        releaseWatchers.TrySetResult(new JobUpdate(JobState.Failed, ErrorKind: ErrorKind.Unknown));
        await freshSubmitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();
    }

    [Fact]
    public async Task StartRunAsync_RunningStateWriteFails_SupervisorLogsSafelyAndSkipsProvider()
    {
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        var sentinel = new InvalidOperationException("private prompt and key detail");
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(sentinel));

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        var generationId = Assert.Single(_added).Id;
        await _service.WhenIdleAsync();

        await _generator.DidNotReceive().GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain(_events, generation => generation.Status == GenerationStatus.Running);
        Assert.Empty(_delay.Delays);
        var error = Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Null(error.Exception);
        Assert.DoesNotContain("private prompt", error.Message, StringComparison.Ordinal);
        Assert.Equal(generationId, error.State["GenerationId"]);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Exception == sentinel);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public async Task StartRunAsync_TransientStateWriteRetriesThenSucceeds_LogsAndPublishesOnce(int transientFailures, int expectedDelays)
    {
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        var runningUpdates = 0;
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var generation = call.Arg<Generation>();
            if (generation.Status == GenerationStatus.Running && Interlocked.Increment(ref runningUpdates) <= transientFailures)
            {
                throw new StorageWriteException("temporary storage failure", true, new IOException("sqlite detail"));
            }

            return Task.CompletedTask;
        });

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal(transientFailures + 1, runningUpdates);
        Assert.Equal(Enumerable.Repeat(TimeSpan.FromMilliseconds(100), expectedDelays), _delay.Delays);
        Assert.Single(_events, generation => generation.Status == GenerationStatus.Running);
        Assert.Single(_logger.Entries, entry => entry.Message.Contains("is Running", StringComparison.Ordinal));
        await _generator.Received(1).GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
        await _files.Received(1).SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_TransientStateWriteExhausted_UsesThreeAttemptsAndTwoDelays()
    {
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new StorageWriteException("temporary storage failure", true));

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        await _runs.Received(3).UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>());
        Assert.Equal([TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100)], _delay.Delays);
        Assert.Empty(_events);
        await _generator.DidNotReceive().GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
        var error = Assert.Single(_logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Null(error.Exception);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Exception is StorageWriteException);
    }

    [Fact]
    public async Task StartRunAsync_HandlePersistenceFailure_CancelsSubmittedJobOnce()
    {
        var handle = Handle();
        var queued = Substitute.For<IQueuedImageProvider>();
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(queued);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(handle);
        queued.CancelAsync(handle, Arg.Any<CancellationToken>()).Returns(CancelOutcome.CancellationRequested);
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<Generation>().JobJson is not null)
            {
                throw new StorageWriteException("temporary handle write failure", true);
            }

            return Task.CompletedTask;
        });

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        await queued.Received(1).CancelAsync(handle, Arg.Any<CancellationToken>());
        await queued.DidNotReceive().PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain(_events, generation => generation.Status == GenerationStatus.Succeeded);
    }

    [Fact]
    public async Task StartRunAsync_HandlePersistenceFailureRacingSessionStop_CancelsRemoteJobOnce()
    {
        var handle = Handle();
        var queued = Substitute.For<IQueuedImageProvider>();
        var handleWriteEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandleWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstHandleWrite = 0;
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(queued);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(handle);
        queued.CancelAsync(handle, Arg.Any<CancellationToken>()).Returns(CancelOutcome.CancellationRequested);
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (call.Arg<Generation>().JobJson is not null && Interlocked.Exchange(ref firstHandleWrite, 1) == 0)
            {
                handleWriteEntered.TrySetResult();
                await releaseHandleWrite.Task;
                throw new StorageWriteException("temporary handle write failure", true);
            }

            if (call.Arg<Generation>().JobJson is not null)
            {
                throw new StorageWriteException("temporary handle write failure", true);
            }

            await Task.CompletedTask;
        });

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await handleWriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var stop = _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);
        releaseHandleWrite.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        await queued.Received(1).CancelAsync(handle, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartRunAsync_TerminalSuccessPersistenceFailure_DeletesOnlyNewFileWithoutSuccessEvent(bool localAdapter)
    {
        const string path = "images/session/new-result.png";
        var succeededWrites = 0;
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _files.SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>())
            .Returns(path);
        if (localAdapter)
        {
            _files.DeleteFileAsync(path, CancellationToken.None)
                .Returns(Task.FromException(new IOException("cleanup detail")));
        }
        if (localAdapter)
        {
            _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(
                new InMemoryQueuedImageProvider(_generator, ProviderIds.OpenRouter, TimeProvider.System));
        }
        else
        {
            var queued = Substitute.For<IQueuedImageProvider>();
            var handle = new ProviderJobHandle(ProviderIds.OpenRouter, "req-1", "https://q/s", "https://q/r", "https://q/c");
            queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(handle);
            queued.PollAsync(handle, Arg.Any<CancellationToken>()).Returns(new JobUpdate(
                JobState.Succeeded, Result: new ImageGenerationResult(Image, "image/png", 0.014m)));
            _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(queued);
        }

        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<Generation>().Status == GenerationStatus.Succeeded)
            {
                succeededWrites++;
                throw new StorageWriteException("terminal write failed", false);
            }

            return Task.CompletedTask;
        });

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal(1, succeededWrites);
        await _files.Received(1).DeleteFileAsync(path, CancellationToken.None);
        Assert.Null(Assert.Single(_added).ImagePath);
        Assert.Null(Assert.Single(_added).MediaType);
        Assert.DoesNotContain(_events, generation => generation.Status == GenerationStatus.Succeeded);
        await _files.DidNotReceive().DeleteSessionFilesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Exception is StorageWriteException);
        if (localAdapter)
        {
            Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Could not clean up image file", StringComparison.Ordinal));
            Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Exception is IOException);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task StartRunAsync_TerminalSuccessTransientWriteFailures_RetriesBeforeCompensation(int transientFailures)
    {
        const string path = "images/session/retry-result.png";
        var succeededWrites = 0;
        var queued = Substitute.For<IQueuedImageProvider>();
        var handle = new ProviderJobHandle(ProviderIds.OpenRouter, "req-1", "https://q/s", "https://q/r", "https://q/c");
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(queued);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(handle);
        queued.PollAsync(handle, Arg.Any<CancellationToken>()).Returns(new JobUpdate(
            JobState.Succeeded, Result: new ImageGenerationResult(Image, "image/png", 0.014m)));
        _files.SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>()).Returns(path);
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<Generation>().Status == GenerationStatus.Succeeded && ++succeededWrites <= transientFailures)
            {
                throw new StorageWriteException("terminal write temporarily failed", true);
            }

            return Task.CompletedTask;
        });

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        var maxRetries = Math.Min(transientFailures, 2);
        Assert.Equal(maxRetries + 1, succeededWrites);
        Assert.Equal(Enumerable.Repeat(TimeSpan.FromMilliseconds(100), maxRetries), _delay.Delays);
        await queued.Received(1).SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
        await _files.Received(1).SaveAsync(_session.Id, Arg.Any<Guid>(), Image, "image/png", Arg.Any<CancellationToken>());
        if (transientFailures < 3)
        {
            await _files.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            Assert.Single(_events, generation => generation.Status == GenerationStatus.Succeeded);
        }
        else
        {
            await _files.Received(1).DeleteFileAsync(path, CancellationToken.None);
            Assert.DoesNotContain(_events, generation => generation.Status == GenerationStatus.Succeeded);
            Assert.Null(Assert.Single(_added).ImagePath);
        }
    }

    [Fact]
    public async Task StopSessionAsync_CancellationDuringTerminalPersistence_WaitsForFileCompensation()
    {
        const string path = "images/session/cancelled-result.png";
        var queued = Substitute.For<IQueuedImageProvider>();
        var handle = new ProviderJobHandle(ProviderIds.OpenRouter, "req-1", "https://q/s", "https://q/r", "https://q/c");
        var terminalWriteEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTerminalWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(queued);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(handle);
        queued.PollAsync(handle, Arg.Any<CancellationToken>()).Returns(new JobUpdate(
            JobState.Succeeded, Result: new ImageGenerationResult(Image, "image/png", 0.014m)));
        queued.CancelAsync(handle, Arg.Any<CancellationToken>()).Returns(CancelOutcome.CancellationRequested);
        _files.SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>()).Returns(path);
        _files.DeleteFileAsync(path, CancellationToken.None).Returns(async _ =>
        {
            cleanupEntered.TrySetResult();
            await releaseCleanup.Task;
        });
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (call.Arg<Generation>().Status == GenerationStatus.Succeeded)
            {
                terminalWriteEntered.TrySetResult();
                await releaseTerminalWrite.Task;
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
            }
        });

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await terminalWriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var stopping = _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);
        releaseTerminalWrite.TrySetResult();
        await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(stopping.IsCompleted);
        await _files.Received(1).DeleteFileAsync(path, CancellationToken.None);
        releaseCleanup.TrySetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();
        Assert.DoesNotContain(_events, generation => generation.Status == GenerationStatus.Succeeded);
    }

    [Fact]
    public async Task StartRunAsync_SuccessEventHandlerThrows_LeavesPersistedImageFile()
    {
        const string path = "images/session/confirmed-result.png";
        var queued = Substitute.For<IQueuedImageProvider>();
        var handle = new ProviderJobHandle(ProviderIds.OpenRouter, "req-1", "https://q/s", "https://q/r", "https://q/c");
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(queued);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(handle);
        queued.PollAsync(handle, Arg.Any<CancellationToken>()).Returns(new JobUpdate(
            JobState.Succeeded, Result: new ImageGenerationResult(Image, "image/png", 0.014m)));
        _files.SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>()).Returns(path);
        var succeededWrites = 0;
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<Generation>().Status == GenerationStatus.Succeeded)
            {
                succeededWrites++;
            }

            return Task.CompletedTask;
        });
        _service.GenerationUpdated += (_, eventArgs) =>
        {
            if (eventArgs.Generation.Status == GenerationStatus.Succeeded)
            {
                throw new InvalidOperationException("subscriber failed");
            }
        };

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal(1, succeededWrites);
        await _files.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Single(_events, generation => generation.Status == GenerationStatus.Succeeded);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("handler", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartRunAsync_ImageSaveThrows_MarksGenerationFailedWithoutInventingCleanupPath()
    {
        var queued = Substitute.For<IQueuedImageProvider>();
        var handle = new ProviderJobHandle(ProviderIds.OpenRouter, "req-1", "https://q/s", "https://q/r", "https://q/c");
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(queued);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(handle);
        queued.PollAsync(handle, Arg.Any<CancellationToken>()).Returns(new JobUpdate(
            JobState.Succeeded, Result: new ImageGenerationResult(Image, "image/png", 0.014m)));
        _files.SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new IOException("save failed before path was returned"));

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        var failed = Assert.Single(_events, generation => generation.Status == GenerationStatus.Failed);
        Assert.Equal(ErrorKind.ImageSaveFailed, failed.ErrorKind);
        Assert.Equal("Could not save the generated image. Check free space and folder permissions.", failed.ErrorMessage);
        Assert.Equal(0.014m, failed.CostUsd);
        await _files.Received(1).SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>());
        await _files.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _files.DidNotReceive().DeleteSessionFilesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain(_events, generation => generation.Status == GenerationStatus.Succeeded);
    }

    [Fact]
    public async Task StartRunAsync_NativeQueuedImageSaveThrows_MarksGenerationFailedWithoutResubmitting()
    {
        var queued = Substitute.For<IQueuedImageProvider>();
        var handle = new ProviderJobHandle(ProviderIds.OpenRouter, "req-1", "https://q/s", "https://q/r", "https://q/c");
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(queued);
        queued.SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(handle);
        queued.PollAsync(handle, Arg.Any<CancellationToken>()).Returns(new JobUpdate(
            JobState.Succeeded, Result: new ImageGenerationResult(Image, "image/png", 0.014m)));
        _files.SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new UnauthorizedAccessException("private path detail"));

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        var failed = Assert.Single(_events, generation => generation.Status == GenerationStatus.Failed);
        Assert.Equal(ErrorKind.ImageSaveFailed, failed.ErrorKind);
        Assert.Equal(0.014m, failed.CostUsd);
        Assert.DoesNotContain("private path detail", failed.ErrorMessage, StringComparison.Ordinal);
        await queued.Received(1).SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
        await queued.Received(1).PollAsync(handle, Arg.Any<CancellationToken>());
        await _files.Received(1).SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>());
        await _files.DidNotReceive().DeleteFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_ImageSaveFailureAndFailedTerminalWrite_DoesNotRetryTerminalWrite()
    {
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _files.SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new IOException("disk is full"));
        var failedWrites = 0;
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<Generation>().Status == GenerationStatus.Failed)
            {
                failedWrites++;
                throw new StorageWriteException("database unavailable", false);
            }

            return Task.CompletedTask;
        });

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal(1, failedWrites);
        Assert.DoesNotContain(_events, generation => generation.Status == GenerationStatus.Failed);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is null);
    }

    [Fact]
    public async Task StartRunAsync_OwnedCancellationDuringImageSave_DoesNotBecomeSaveFailure()
    {
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _files.SaveAsync(_session.Id, Arg.Any<Guid>(), Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                saveEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
                return "unused";
            });

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await saveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.DoesNotContain(_events, generation => generation.Status == GenerationStatus.Failed);
        await _runs.DidNotReceive().UpdateGenerationAsync(
            Arg.Is<Generation>(generation => generation.ErrorKind == ErrorKind.ImageSaveFailed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_PermanentStateWriteFailure_IsNotRetried()
    {
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new StorageWriteException("missing generation", false));

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        await _runs.Received(1).UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>());
        Assert.Empty(_delay.Delays);
        await _generator.DidNotReceive().GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_CancelDuringStateRetryDelay_DoesNotRetryAgain()
    {
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        var delayEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _delay.WaitForCancellation(delayEntered);
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new StorageWriteException("temporary storage failure", true));

        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);

        await _runs.Received(1).UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>());
        Assert.Single(_delay.Delays);
        Assert.DoesNotContain(_logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task StartRunAsync_PreparationFailureReleasesStartLease()
    {
        _session.Settings.ImageModels = [];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken));

        await _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await _runs.DidNotReceive().AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopSessionAsync_WaitsForAdmittedStartThenCancelsItsJobs()
    {
        var addEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAdd = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _runs.AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                addEntered.TrySetResult();
                await allowAdd.Task.WaitAsync(call.Arg<CancellationToken>());
            });
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>())
                .ContinueWith<ImageGenerationResult>(_ => new ImageGenerationResult(Image, "image/png", 0.014m), TaskScheduler.Default));

        var start = _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await addEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var stop = _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);
        Assert.False(stop.IsCompleted);
        allowAdd.TrySetResult();
        await start;
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var eventCount = _events.Count;
        await _service.WhenIdleAsync();
        Assert.Equal(eventCount, _events.Count);
    }

    [Fact]
    public async Task StartRunAsync_CallerCancellationAfterReturn_DoesNotCancelJobs()
    {
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new ConcurrentQueue<CancellationToken>();
        var calls = 0;
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                tokens.Enqueue(call.Arg<CancellationToken>());
                if (Interlocked.Increment(ref calls) == 4)
                {
                    entered.TrySetResult();
                }

                return await release.Task.WaitAsync(call.Arg<CancellationToken>());
            });

        await _service.StartRunAsync(_session.Id, "fox", caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await caller.CancelAsync();

        Assert.All(tokens, token => Assert.False(token.IsCancellationRequested));
        release.TrySetResult(new ImageGenerationResult(Image, "image/png", 0.014m));
        await _service.WhenIdleAsync();
        Assert.Equal(4, _added.Count(g => g.Status == GenerationStatus.Succeeded));
    }

    [Fact]
    public async Task StopSessionAsync_CancelledWaiterDoesNotCancelStopOperation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                if (Interlocked.Increment(ref calls) == 4)
                {
                    entered.TrySetResult();
                }

                return await release.Task;
            });
        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var waiter = new CancellationTokenSource();
        var firstWait = _service.StopSessionAsync(_session.Id, waiter.Token);
        await waiter.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstWait);
        var secondWait = _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);
        Assert.False(secondWait.IsCompleted);

        release.TrySetResult(new ImageGenerationResult(Image, "image/png", 0.014m));
        await secondWait.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _files.DidNotReceive().SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.DoesNotContain(_logger.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task StopSessionAsync_SupervisedPersistenceFailureDoesNotFaultBackgroundTasks()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                entered.TrySetResult();
                return Task.FromException(new IOException("job cleanup failed"));
            });
        await _service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await _service.WhenIdleAsync();
        await _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);

        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is null);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Exception is IOException);
    }

    [Fact]
    public async Task StopSessionAsync_OnlyCancelsJobsOfTargetSession()
    {
        var otherSession = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Other",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = new SessionSettings
            {
                ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)],
                RunsPerModel = 1,
                AssistantModel = new ModelRef(ProviderIds.OpenRouter, SessionSettings.DefaultAssistantModelId),
            },
        };
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        _sessions.GetAsync(otherSession.Id, Arg.Any<CancellationToken>()).Returns(otherSession);
        var allEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new ConcurrentQueue<CancellationToken>();
        var calls = 0;
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                tokens.Enqueue(call.Arg<CancellationToken>());
                if (Interlocked.Increment(ref calls) == 2)
                {
                    allEntered.TrySetResult();
                }

                return await release.Task.WaitAsync(call.Arg<CancellationToken>());
            });

        await _service.StartRunAsync(_session.Id, "first", TestContext.Current.CancellationToken);
        await _service.StartRunAsync(otherSession.Id, "second", TestContext.Current.CancellationToken);
        await allEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);

        var observedTokens = tokens.ToArray();
        Assert.Equal(2, observedTokens.Length);
        Assert.Single(observedTokens, token => token.IsCancellationRequested);
        Assert.Single(observedTokens, token => !token.IsCancellationRequested);
        release.TrySetResult(new ImageGenerationResult(Image, "image/png", 0.014m));
        await _service.WhenIdleAsync();
    }

    [Fact]
    public async Task RecoverInterruptedAsync_TombstonedSession_SkipsPoll()
    {
        var generation = ResumableGeneration();
        var queued = QueuedProviderThatFails(new ProviderException(ErrorKind.InvalidKey, "unused"));
        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([generation]);
        _providers.GetQueuedImageProvider("fal").Returns(queued);
        await _service.StopSessionAsync(_session.Id, TestContext.Current.CancellationToken);

        await _service.RecoverInterruptedAsync(TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        await queued.DidNotReceive().PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>());
        await queued.DidNotReceive().SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverInterruptedAsync_ConcurrentRecovery_RegistersWatcherOnlyOnce()
    {
        var generation = ResumableGeneration();
        var pollEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<JobUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = Handle();
        generation.JobJson = System.Text.Json.JsonSerializer.Serialize(handle);
        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([generation]);
        var connection = Substitute.For<IProviderConnection, IQueuedImageProvider>();
        var queued = (IQueuedImageProvider)connection;
        queued.PollAsync(handle, Arg.Any<CancellationToken>()).Returns(call =>
        {
            pollEntered.TrySetResult();
            return release.Task;
        });
        _providers.GetQueuedImageProvider("fal").Returns((IQueuedImageProvider)connection);

        await _service.RecoverInterruptedAsync(TestContext.Current.CancellationToken);
        await pollEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _service.RecoverInterruptedAsync(TestContext.Current.CancellationToken);

        await queued.Received(1).PollAsync(handle, Arg.Any<CancellationToken>());
        release.TrySetResult(new JobUpdate(JobState.Failed, ErrorKind: ErrorKind.Unknown, ErrorMessage: "done"));
        await _service.WhenIdleAsync();
    }

    public GenerationServiceTests()
    {
        _providers.GetImageProvider(ProviderIds.OpenRouter).Returns(Substitute.For<IModelProvider>());
        _providers.ImageProviderIds.Returns([ProviderIds.OpenRouter]);
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [
                ModelCatalogServiceTests.Model(Seeded, "Seeded"),
                ModelCatalogServiceTests.Model(Unseeded, "Unseeded", supportsSeed: false),
            ]);
        var catalog = new ModelCatalogService(_providers, _modelRepository, NullLogger<ModelCatalogService>.Instance);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);

        _seeds.NextSeed().Returns(11L, 22L, 33L);
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ImageGenerationResult(Image, "image/png", 0.014m));
        _providers.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(new InMemoryQueuedImageProvider(_generator, ProviderIds.OpenRouter, time));
        _files.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("images/session/generation.png");
        _runs.AddRunAsync(Arg.Do<Run>(r => _savedRuns[r.Id] = r), Arg.Do<IReadOnlyList<Generation>>(g => _added = g), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _runs.GetRunAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult<Run?>(_savedRuns.TryGetValue(c.Arg<Guid>(), out var run) ? run : DefaultRun(c.Arg<Guid>())));

        _session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Session",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = new SessionSettings
            {
                ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded), new ModelRef(ProviderIds.OpenRouter, Unseeded)],
                RunsPerModel = 2,
                AssistantModel = new ModelRef(ProviderIds.OpenRouter, SessionSettings.DefaultAssistantModelId),
            },
        };
        _sessions.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);

        _attachments = new AttachmentService(_chatAttachments, _files, time);
        _service = new GenerationService(
            _sessions, _runs, _providers, _files, catalog, _seeds, _attachments, time, _logger, _changes, delay: _delay);
        _service.GenerationUpdated += (_, e) => _events.Enqueue(e.Generation);
    }

    /// <summary>Run of a generation the test made by hand: no parameters asked for, as in the old records.</summary>
    private Run DefaultRun(Guid runId) => new()
    {
        Id = runId,
        SessionId = _session.Id,
        Prompt = "fox",
        Origin = RunOrigin.Assistant,
        CreatedAt = Now,
    };

    private static Generation Existing(Guid sessionId, string modelId, long seed, string prompt = "fox") => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        SessionId = sessionId,
        ProviderId = ProviderIds.OpenRouter,
        ModelId = modelId,
        Prompt = prompt,
        Seed = seed,
        SeedApplied = true,
        Status = GenerationStatus.Succeeded,
        CreatedAt = Now,
        SortOrder = 0,
    };

    [Fact]
    public async Task StartRunAsync_TraceOn_RecordsGenerationRequestWithModelsAndSeeds()
    {
        var trace = new CapturingDebugTrace();
        var catalog = new ModelCatalogService(_providers, _modelRepository, NullLogger<ModelCatalogService>.Instance);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var traced = new GenerationService(
            _sessions, _runs, _providers, _files, catalog, _seeds, _attachments, time, _logger, _changes, trace);

        await traced.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);

        var record = Assert.Single(trace.Records, r => r.Kind == "generation-request");
        Assert.Equal("a fox", record.Fields["prompt"]);
        Assert.Equal($"{ProviderIds.OpenRouter}/{Seeded},{ProviderIds.OpenRouter}/{Unseeded}", record.Fields["models"]);
        Assert.Equal("11,22", record.Fields["seeds"]);
    }

    [Fact]
    public async Task StartRunAsync_TwoModelsTwoRuns_CreatesFourGenerationsInOrder()
    {
        var ct = TestContext.Current.CancellationToken;

        var run = await _service.StartRunAsync(_session.Id, "  a fox  ", ct);
        await _service.WhenIdleAsync();

        Assert.Equal(RunOrigin.Assistant, run.Origin);
        Assert.Equal("a fox", run.Prompt);
        Assert.Equal(4, _added.Count);
        Assert.Equal(new[] { (Seeded, 11L), (Seeded, 22L), (Unseeded, 11L), (Unseeded, 22L) },
            _added.Select(g => (g.ModelId, g.Seed)));
        Assert.Equal([0, 1, 2, 3], _added.Select(g => g.SortOrder));
        Assert.All(_added, g => Assert.Equal("a fox", g.Prompt));
        Assert.Equal([true, true, false, false], _added.Select(g => g.SeedApplied));
    }

    [Fact]
    public async Task StartRunAsync_Succeeds_SavesFilesAndNotifiesRunningAndSucceeded()
    {
        var ct = TestContext.Current.CancellationToken;

        await _service.StartRunAsync(_session.Id, "a fox", ct);
        await _service.WhenIdleAsync();

        Assert.Equal(4, _events.Count(e => e.Status == GenerationStatus.Running));
        Assert.Equal(4, _events.Count(e => e.Status == GenerationStatus.Succeeded));
        Assert.All(_added, g =>
        {
            Assert.Equal(GenerationStatus.Succeeded, g.Status);
            Assert.Equal("images/session/generation.png", g.ImagePath);
            Assert.Equal("image/png", g.MediaType);
            Assert.Equal(0.014m, g.CostUsd);
            Assert.Equal(Now, g.CompletedAt);
        });
        await _files.Received(4).SaveAsync(_session.Id, Arg.Any<Guid>(), Image, "image/png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_RealOpenRouterWrapper_UsesGeneratePathWithoutQueuedJobJson()
    {
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded)];
        _session.Settings.RunsPerModel = 1;
        var handler = StubHttpMessageHandler.Json(
            System.Net.HttpStatusCode.OK,
            "{\"data\":[{\"b64_json\":\"AQID\",\"media_type\":\"image/png\"}]}");
        using var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(ProviderIds.OpenRouter).Returns(httpClient);
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.ForProvider(ProviderIds.OpenRouter), Arg.Any<CancellationToken>()).Returns("test-key");
        var registry = ProviderConfigLoader.Build(
            """{"providers":[{"id":"openrouter","kind":"openRouter","displayName":"OpenRouter","baseAddress":"https://openrouter.ai/api/v1/","timeoutSeconds":60}]}""",
            null);
        var router = new ProviderRouter(registry, httpClientFactory, secrets, TimeProvider.System);
        var connection = router.GetConnection(ProviderIds.OpenRouter);
        Assert.IsType<InMemoryQueuedImageConnection>(connection);
        Assert.IsAssignableFrom<IQueuedImageProvider>(connection);
        var catalog = new ModelCatalogService(router, _modelRepository, NullLogger<ModelCatalogService>.Instance);
        var service = new GenerationService(
            _sessions, _runs, router, _files, catalog, _seeds, _attachments, TimeProvider.System, _logger, _changes);

        await service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await service.WhenIdleAsync();

        Assert.NotNull(Assert.Single(_added).JobJson);
        Assert.Single(handler.Requests);
        Assert.Equal("https://openrouter.ai/api/v1/images", handler.Requests.Single().RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task StartRunAsync_BuiltInTogether_UsesConfiguredImageContractAndKeepsCostUnknown()
    {
        const string togetherId = "together";
        const string modelId = "black-forest-labs/FLUX.2-dev";
        const string imageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADUlEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";
        var image = Convert.FromBase64String(imageBase64);
        var handler = StubHttpMessageHandler.Json(
            System.Net.HttpStatusCode.OK,
            $$"""{"data":[{"b64_json":"{{imageBase64}}"}]}""");
        using var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(togetherId).Returns(httpClient);
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.ForProvider(togetherId), Arg.Any<CancellationToken>()).Returns("test-key");
        var router = new ProviderRouter(
            ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null),
            httpClientFactory,
            secrets,
            TimeProvider.System);
        _session.Settings.ImageModels = [new ModelRef(togetherId, modelId)];
        _session.Settings.RunsPerModel = 1;
        var catalog = new ModelCatalogService(router, _modelRepository, NullLogger<ModelCatalogService>.Instance);
        var service = new GenerationService(
            _sessions, _runs, router, _files, catalog, _seeds, _attachments, TimeProvider.System, _logger, _changes);

        await service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await service.WhenIdleAsync();

        var generation = Assert.Single(_added);
        Assert.Equal(GenerationStatus.Succeeded, generation.Status);
        Assert.Null(generation.CostUsd);
        Assert.Equal("image/png", generation.MediaType);
        await _files.Received(1).SaveAsync(
            _session.Id, generation.Id, Arg.Is<byte[]>(bytes => bytes.SequenceEqual(image)), "image/png", Arg.Any<CancellationToken>());
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.together.ai/v1/images/generations", request.RequestUri!.AbsoluteUri);
        Assert.StartsWith("Bearer ", request.Headers.Authorization!.ToString(), StringComparison.Ordinal);
        using var body = System.Text.Json.JsonDocument.Parse(Assert.Single(handler.RequestBodies));
        var root = body.RootElement;
        Assert.Equal(modelId, root.GetProperty("model").GetString());
        Assert.Equal("fox", root.GetProperty("prompt").GetString());
        Assert.Equal(1, root.GetProperty("n").GetInt32());
        Assert.Equal("base64", root.GetProperty("response_format").GetString());
        Assert.Equal("png", root.GetProperty("output_format").GetString());
        Assert.Equal(11, root.GetProperty("seed").GetInt64());
    }

    [Fact]
    public async Task StartRunAsync_BuiltInTogetherWithoutKey_FailsInvalidKeyWithoutHttpCall()
    {
        const string togetherId = "together";
        const string modelId = "black-forest-labs/FLUX.2-dev";
        var handler = StubHttpMessageHandler.Json(System.Net.HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(togetherId).Returns(httpClient);
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.ForProvider(togetherId), Arg.Any<CancellationToken>()).Returns((string?)null);
        var router = new ProviderRouter(
            ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null),
            httpClientFactory,
            secrets,
            TimeProvider.System);
        _session.Settings.ImageModels = [new ModelRef(togetherId, modelId)];
        _session.Settings.RunsPerModel = 1;
        var catalog = new ModelCatalogService(router, _modelRepository, NullLogger<ModelCatalogService>.Instance);
        var service = new GenerationService(
            _sessions, _runs, router, _files, catalog, _seeds, _attachments, TimeProvider.System, _logger, _changes);

        await service.StartRunAsync(_session.Id, "fox", TestContext.Current.CancellationToken);
        await service.WhenIdleAsync();

        var generation = Assert.Single(_added);
        Assert.Equal(GenerationStatus.Failed, generation.Status);
        Assert.Equal(ErrorKind.InvalidKey, generation.ErrorKind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task StartRunAsync_Succeeds_LogsQueuedRunningSucceededForEachGeneration()
    {
        var ct = TestContext.Current.CancellationToken;

        await _service.StartRunAsync(_session.Id, "a fox", ct);
        await _service.WhenIdleAsync();

        var first = _added[0];
        var messages = _logger.Entries
            .Where(e => e.Message.Contains(first.Id.ToString(), StringComparison.Ordinal))
            .Select(e => e.Message)
            .ToList();
        Assert.Contains(messages, message => message.Contains("is Queued", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("is Running", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("is Succeeded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartRunAsync_Moderated_LogsFilteredAtInformationNotWarning()
    {
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ImageGenerationResult>(new ProviderException(ErrorKind.Moderated, "blocked")));

        await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        var filtered = _logger.Entries.Where(e => e.Message.Contains("is Filtered", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(filtered);
        Assert.All(filtered, e => Assert.Equal(LogLevel.Information, e.Level));
    }

    [Fact]
    public async Task StartRunAsync_Succeeds_DoesNotLogPromptAtInformation()
    {
        await _service.StartRunAsync(_session.Id, "a secret fox prompt", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("secret fox prompt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartRunAsync_ParameterNotSupportedByModel_IsDroppedAndNotSent()
    {
        _session.Settings.Parameters = new GenerationParameters { AspectRatio = "3:2" };
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [
                ModelWithParameters(Seeded, "output_format", "png"),
                ModelWithParameters(Unseeded, "output_format", "png"),
            ]);

        await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        var outcome = Assert.Single(_added[0].Outcomes);
        Assert.Equal(ParameterOutcomeKind.Dropped, outcome.Kind);
        Assert.Equal(ParameterDropReason.NotSupported, outcome.Reason);
        await _generator.Received(4).GenerateAsync(
            Arg.Is<ImageGenerationRequest>(r => r.AspectRatio == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_TwoModels_EachGenerationHasOwnOutcomesAndSameSeed()
    {
        _session.Settings.ImageModels = [new ModelRef(ProviderIds.OpenRouter, Seeded), new ModelRef(ProviderIds.OpenRouter, Unseeded)];
        _session.Settings.RunsPerModel = 1;
        _session.Settings.Parameters = new GenerationParameters { OutputFormat = "webp" };
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [
                ModelWithParameters(Seeded, "output_format", "png", "webp"),
                ModelWithParameters(Unseeded, "output_format", "png"),
            ]);

        await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal(ParameterOutcomeKind.Applied, _added[0].Outcomes.Single().Kind);
        Assert.Equal(ParameterOutcomeKind.Dropped, _added[1].Outcomes.Single().Kind);
        Assert.Equal(_added[0].Seed, _added[1].Seed);
    }

    [Fact]
    public async Task RepeatAsync_AfterSessionParametersChanged_UsesParametersOfSourceRun()
    {
        var ct = TestContext.Current.CancellationToken;
        _session.Settings.Parameters = new GenerationParameters { OutputFormat = "png" };
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [ModelWithParameters(Seeded, "output_format", "png", "webp")]);
        var source = await _service.StartRunAsync(_session.Id, "a fox", ct);
        await _service.WhenIdleAsync();
        var sourceGeneration = _added[0];
        _runs.GetGenerationAsync(sourceGeneration.Id, Arg.Any<CancellationToken>()).Returns(sourceGeneration);
        _runs.GetRunAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _session.Settings.Parameters = new GenerationParameters { OutputFormat = "webp" };

        await _service.RepeatAsync(sourceGeneration.Id, ct);
        await _service.WhenIdleAsync();

        Assert.Equal("png", _added.Single().Outcomes.Single().Sent);
    }

    private static ImageModel ModelWithParameters(string id, string parameterName, params string[] values) =>
        new()
        {
            ProviderId = ProviderIds.OpenRouter,
            Id = id,
            Name = id,
            SupportsSeed = id == Seeded,
            SupportsImageInput = false,
            MaxImagesPerRequest = 1,
            FetchedAt = DateTimeOffset.UnixEpoch,
            Parameters = [new ModelParameter(parameterName, ParameterKind.Enum, values)],
        };

    [Fact]
    public async Task StartRunWithAttachmentsAsync_ModelWithoutReferences_IsSkippedAndNoRunCreated()
    {
        var attachment = Attachment();

        var result = await _service.StartRunWithAttachmentsAsync(_session.Id, "a fox", [attachment.Id], TestContext.Current.CancellationToken);

        Assert.Null(result.Run);
        Assert.Equal([ModelSkipReason.NoReferenceSupport, ModelSkipReason.NoReferenceSupport], result.SkippedModels.Select(s => s.Reason));
        Assert.Empty(_added);
    }

    [Fact]
    public async Task StartRunWithAttachmentsAsync_ModelWithOneReference_SendsFirstAttachmentOnly()
    {
        var first = Attachment();
        var second = Attachment();
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [ModelWithReferences(Seeded, 1), ModelWithReferences(Unseeded, 1)]);

        var result = await _service.StartRunWithAttachmentsAsync(_session.Id, "a fox", [first.Id, second.Id], TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Empty(result.SkippedModels);
        Assert.Equal(1, result.ReferencesNotSent);
        Assert.All(_added, g => Assert.Equal([first.Id], g.ReferenceAttachmentIds));
        await _generator.Received().GenerateAsync(
            Arg.Is<ImageGenerationRequest>(r => r.ReferenceImageDataUrls!.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunWithAttachmentsAsync_OneModelWithReferencesOneWithout_CreatesOnlyTheCapableOne()
    {
        var attachment = Attachment();
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [ModelWithReferences(Seeded, 2), ModelWithReferences(Unseeded, 0)]);

        var result = await _service.StartRunWithAttachmentsAsync(_session.Id, "a fox", [attachment.Id], TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal(ModelSkipReason.NoReferenceSupport, Assert.Single(result.SkippedModels).Reason);
        Assert.Equal(Unseeded, result.SkippedModels[0].Model.ModelId);
        Assert.All(_added, g => Assert.Equal(Seeded, g.ModelId));
    }

    [Fact]
    public async Task RepeatAsync_GenerationWithReference_KeepsSameReference()
    {
        var attachment = Attachment();
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [ModelWithReferences(Seeded, 1), ModelWithReferences(Unseeded, 1)]);
        var result = await _service.StartRunWithAttachmentsAsync(_session.Id, "a fox", [attachment.Id], TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();
        var source = _added[0];
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _runs.GetRunAsync(result.Run!.Id, Arg.Any<CancellationToken>()).Returns(result.Run);

        await _service.RepeatAsync(source.Id, TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal([attachment.Id], _added.Single().ReferenceAttachmentIds);
    }

    [Fact]
    public async Task StartRunWithAttachmentsAsync_ReferenceFileMissing_GenerationFailedOthersRun()
    {
        var attachment = Attachment();
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [ModelWithReferences(Seeded, 1), ModelWithReferences(Unseeded, 1)]);
        _files.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<byte[]>(_ => throw new FileNotFoundException("gone"));

        await _service.StartRunWithAttachmentsAsync(_session.Id, "a fox", [attachment.Id], TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.All(_added, g =>
        {
            Assert.Equal(GenerationStatus.Failed, g.Status);
            Assert.Equal(ErrorKind.ReferenceUnreadable, g.ErrorKind);
        });
    }

    private ChatAttachment Attachment()
    {
        var attachment = new ChatAttachment
        {
            Id = Guid.NewGuid(),
            SessionId = _session.Id,
            RelativePath = $"images/{_session.Id:N}/attachments/{Guid.NewGuid():N}.png",
            MediaType = "image/png",
            CreatedAt = Now,
        };
        _chatAttachments.GetAsync(attachment.Id, Arg.Any<CancellationToken>()).Returns(attachment);
        _files.ReadAsync(attachment.RelativePath, Arg.Any<CancellationToken>()).Returns([1, 2, 3]);
        return attachment;
    }

    private static ImageModel ModelWithReferences(string id, int maxReferences) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = id,
        SupportsSeed = id == Seeded,
        SupportsImageInput = maxReferences > 0,
        MaxImagesPerRequest = 1,
        FetchedAt = DateTimeOffset.UnixEpoch,
        Parameters = [new ModelParameter(ReferenceImageLimit.ParameterName, ParameterKind.Range, [], 0, maxReferences)],
    };

    [Fact]
    public async Task StartRunAsync_SeedSentOnlyForSeededModels()
    {
        var ct = TestContext.Current.CancellationToken;

        await _service.StartRunAsync(_session.Id, "a fox", ct);
        await _service.WhenIdleAsync();

        await _generator.Received(2).GenerateAsync(Arg.Is<ImageGenerationRequest>(r => r.Seed == null), Arg.Any<CancellationToken>());
        await _generator.Received(1).GenerateAsync(Arg.Is<ImageGenerationRequest>(r => r.ModelId == Seeded && r.Seed == 11), Arg.Any<CancellationToken>());
        await _generator.Received(1).GenerateAsync(Arg.Is<ImageGenerationRequest>(r => r.ModelId == Seeded && r.Seed == 22), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_Moderated_AllGenerationsFiltered()
    {
        var ct = TestContext.Current.CancellationToken;
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ImageGenerationResult>(new ProviderException(ErrorKind.Moderated, "blocked")));

        await _service.StartRunAsync(_session.Id, "a fox", ct);
        await _service.WhenIdleAsync();

        Assert.All(_added, g =>
        {
            Assert.Equal(GenerationStatus.Filtered, g.Status);
            Assert.Equal(ErrorKind.Moderated, g.ErrorKind);
            Assert.Equal("blocked", g.ErrorMessage);
            Assert.Null(g.ImagePath);
        });
        Assert.Equal(4, _events.Count(e => e.Status == GenerationStatus.Filtered));
        await _files.DidNotReceive().SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_OneModelRateLimited_OnlyItsGenerationsFail()
    {
        var ct = TestContext.Current.CancellationToken;
        _generator.GenerateAsync(Arg.Is<ImageGenerationRequest>(r => r.ModelId == Unseeded), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ImageGenerationResult>(new ProviderException(ErrorKind.RateLimited, "slow down")));

        await _service.StartRunAsync(_session.Id, "a fox", ct);
        await _service.WhenIdleAsync();

        var failed = _added.Where(g => g.ModelId == Unseeded).ToList();
        Assert.Equal(2, failed.Count);
        Assert.All(failed, g =>
        {
            Assert.Equal(GenerationStatus.Failed, g.Status);
            Assert.Equal(ErrorKind.RateLimited, g.ErrorKind);
            Assert.Equal("slow down", g.ErrorMessage);
        });
        Assert.All(_added.Where(g => g.ModelId == Seeded), g => Assert.Equal(GenerationStatus.Succeeded, g.Status));
    }

    [Fact]
    public async Task StartRunAsync_ModelOfUnconfiguredProvider_FailsWithoutCallingAnyProvider()
    {
        var ct = TestContext.Current.CancellationToken;
        _session.Settings.ImageModels = [new ModelRef("fal", "fal-ai/flux/dev")];
        _providers.GetQueuedImageProvider("fal").Returns<IQueuedImageProvider>(_ => throw new ProviderException(ErrorKind.ModelUnavailable, "Provider 'fal' is not configured."));

        await _service.StartRunAsync(_session.Id, "a fox", ct);
        await _service.WhenIdleAsync();

        Assert.All(_added, g =>
        {
            Assert.Equal("fal", g.ProviderId);
            Assert.Equal(GenerationStatus.Failed, g.Status);
            Assert.Equal(ErrorKind.ModelUnavailable, g.ErrorKind);
        });
        await _generator.DidNotReceive().GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepeatAsync_SourceFromOtherProvider_KeepsProviderOfSource()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = Existing(_session.Id, "sd-xl", seed: 33);
        source.ProviderId = "local";
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        await _service.RepeatAsync(source.Id, ct);
        await _service.WhenIdleAsync();

        Assert.Equal([("local", "sd-xl", 33L)], _added.Select(g => (g.ProviderId, g.ModelId, g.Seed)));
    }

    [Fact]
    public async Task StartRunAsync_NoImageModels_ThrowsAndCreatesNothing()
    {
        _session.Settings.ImageModels = [];

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken));

        await _runs.DidNotReceive().AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_UnknownSession_Throws()
    {
        var id = Guid.NewGuid();
        _sessions.GetAsync(id, Arg.Any<CancellationToken>()).Returns((Session?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartRunAsync(id, "a fox", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartRunAsync_BlankPrompt_ThrowsArgumentException(string prompt)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.StartRunAsync(_session.Id, prompt, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RepeatAsync_SamePromptModelAndSeed()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = Existing(_session.Id, Seeded, seed: 33);
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var run = await _service.RepeatAsync(source.Id, ct);
        await _service.WhenIdleAsync();

        Assert.Equal(RunOrigin.Repeat, run.Origin);
        Assert.Equal("fox", run.Prompt);
        Assert.Equal([(Seeded, 33L)], _added.Select(g => (g.ModelId, g.Seed)));
        Assert.Equal([true], _added.Select(g => g.SeedApplied));
        _seeds.DidNotReceive().NextSeed();
    }

    [Fact]
    public async Task NewSeedAsync_SameModelAndPromptNewSeed()
    {
        var ct = TestContext.Current.CancellationToken;
        _seeds.NextSeed().Returns(44L);
        var source = Existing(_session.Id, Seeded, seed: 33);
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var run = await _service.NewSeedAsync(source.Id, ct);
        await _service.WhenIdleAsync();

        Assert.Equal(RunOrigin.NewSeed, run.Origin);
        Assert.Equal("fox", run.Prompt);
        Assert.Equal([(Seeded, 44L)], _added.Select(g => (g.ModelId, g.Seed)));
    }

    [Fact]
    public async Task EditAsync_NewTrimmedPromptSameModelAndSeed()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = Existing(_session.Id, Seeded, seed: 33);
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var run = await _service.EditAsync(source.Id, "  a red fox  ", ct);
        await _service.WhenIdleAsync();

        Assert.Equal(RunOrigin.Edit, run.Origin);
        Assert.Equal("a red fox", run.Prompt);
        Assert.Equal([(Seeded, 33L, "a red fox")], _added.Select(g => (g.ModelId, g.Seed, g.Prompt)));
    }

    [Fact]
    public async Task RunOnModelAsync_UsesSamePromptAndSeed_NewModel()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = Existing(_session.Id, Seeded, seed: 33, prompt: "a red fox");
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var run = await _service.RunOnModelAsync(source.Id, new ModelRef(ProviderIds.OpenRouter, Unseeded), ct);
        await _service.WhenIdleAsync();

        Assert.Equal("a red fox", run.Prompt);
        Assert.Equal([(Unseeded, 33L, "a red fox")], _added.Select(g => (g.ModelId, g.Seed, g.Prompt)));
        _seeds.DidNotReceive().NextSeed();
    }

    [Fact]
    public async Task RunOnModelAsync_ModelWithoutSeed_DoesNotSendSeed()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = Existing(_session.Id, Seeded, seed: 33);
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        await _service.RunOnModelAsync(source.Id, new ModelRef(ProviderIds.OpenRouter, Unseeded), ct);
        await _service.WhenIdleAsync();

        Assert.Equal([false], _added.Select(g => g.SeedApplied));
        await _generator.Received(1).GenerateAsync(
            Arg.Is<ImageGenerationRequest>(r => r.ModelId == Unseeded && r.Seed == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnModelAsync_SetsOtherModelOrigin()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = Existing(_session.Id, Seeded, seed: 33);
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var run = await _service.RunOnModelAsync(source.Id, new ModelRef(ProviderIds.OpenRouter, Seeded), ct);
        await _service.WhenIdleAsync();

        Assert.Equal(RunOrigin.OtherModel, run.Origin);
    }

    [Fact]
    public async Task RunOnModelAsync_TargetDoesNotSupportReferences_RejectsBeforeCreatingRun()
    {
        var source = Existing(_session.Id, Seeded, seed: 33);
        source.ReferenceAttachmentIds = [Guid.NewGuid()];
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>())
            .Returns([ModelWithReferences(Unseeded, 0)]);

        var error = await Assert.ThrowsAsync<ProviderException>(() =>
            _service.RunOnModelAsync(source.Id, new ModelRef(ProviderIds.OpenRouter, Unseeded), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.ModelWithoutReferences, error.Kind);
        await _runs.DidNotReceive().AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>());
        await _generator.DidNotReceive().GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnModelAsync_TargetReferenceLimitIsTooSmall_RejectsBeforeCreatingRun()
    {
        var source = Existing(_session.Id, Seeded, seed: 33);
        source.ReferenceAttachmentIds = [Guid.NewGuid(), Guid.NewGuid()];
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>())
            .Returns([ModelWithReferences(Unseeded, 1)]);

        var error = await Assert.ThrowsAsync<ProviderException>(() =>
            _service.RunOnModelAsync(source.Id, new ModelRef(ProviderIds.OpenRouter, Unseeded), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.BadRequest, error.Kind);
        Assert.Contains("2", error.Message, StringComparison.Ordinal);
        Assert.Contains("1", error.Message, StringComparison.Ordinal);
        await _runs.DidNotReceive().AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>());
        await _generator.DidNotReceive().GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnModelAsync_UnknownTargetWithReferences_RejectsBeforeCreatingRun()
    {
        var source = Existing(_session.Id, Seeded, seed: 33);
        source.ReferenceAttachmentIds = [Guid.NewGuid()];
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var error = await Assert.ThrowsAsync<ProviderException>(() =>
            _service.RunOnModelAsync(source.Id, new ModelRef(ProviderIds.OpenRouter, "vendor/missing"), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.ModelWithoutReferences, error.Kind);
        await _runs.DidNotReceive().AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RunOnModelAsync_TargetSupportsAllReferences_PreservesOrderAndRunSettings(int targetLimit)
    {
        var references = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var source = Existing(_session.Id, Seeded, seed: 33, prompt: "a red fox");
        source.ReferenceAttachmentIds = references;
        var parameters = new GenerationParameters { AspectRatio = "3:2", Resolution = "1K" };
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        _runs.GetRunAsync(source.RunId, Arg.Any<CancellationToken>()).Returns(new Run
        {
            Id = source.RunId,
            SessionId = _session.Id,
            Prompt = source.Prompt,
            Origin = RunOrigin.Assistant,
            CreatedAt = Now,
            Parameters = parameters,
        });
        var target = ModelWithReferences(Unseeded, targetLimit);
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns([target]);
        foreach (var id in references)
        {
            var attachment = new ChatAttachment
            {
                Id = id,
                SessionId = _session.Id,
                RelativePath = $"images/{id:N}.png",
                MediaType = "image/png",
                CreatedAt = Now,
            };
            _chatAttachments.GetAsync(id, Arg.Any<CancellationToken>()).Returns(attachment);
            _files.ReadAsync(attachment.RelativePath, Arg.Any<CancellationToken>()).Returns([1, 2, 3]);
        }

        var run = await _service.RunOnModelAsync(source.Id, new ModelRef(ProviderIds.OpenRouter, Unseeded), TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal("a red fox", run.Prompt);
        Assert.Equal(parameters, run.Parameters);
        Assert.Equal(references, Assert.Single(_added).ReferenceAttachmentIds);
        Assert.Equal(33, Assert.Single(_added).Seed);
        Assert.Equal(RunOrigin.OtherModel, run.Origin);
    }

    [Fact]
    public async Task RepeatAsync_UnknownGeneration_Throws()
    {
        var id = Guid.NewGuid();
        _runs.GetGenerationAsync(id, Arg.Any<CancellationToken>()).Returns((Generation?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RepeatAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecoverInterruptedAsync_PassesCurrentTimeAndReturnsCount()
    {
        var ct = TestContext.Current.CancellationToken;
        _runs.MarkUnfinishedAsInterruptedAsync(Now, Arg.Any<CancellationToken>()).Returns(3);

        var count = await _service.RecoverInterruptedAsync(ct);

        Assert.Equal(3, count);
        await _runs.Received(1).MarkUnfinishedAsInterruptedAsync(Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverInterruptedAsync_QueuedJob_FollowsSavedHandleWithoutSubmitting()
    {
        var ct = TestContext.Current.CancellationToken;
        var handle = new ProviderJobHandle("fal", "req-1", "https://q/s", "https://q/r", "https://q/c");
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = _session.Id,
            ProviderId = "fal",
            ModelId = "fal-ai/flux/dev",
            Prompt = "fox",
            Seed = 1,
            SeedApplied = true,
            Status = GenerationStatus.Running,
            CreatedAt = Now,
            SortOrder = 0,
            JobJson = System.Text.Json.JsonSerializer.Serialize(handle),
        };
        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([generation]);
        var connection = Substitute.For<IProviderConnection, IQueuedImageProvider>();
        var queued = (IQueuedImageProvider)connection;
        queued.PollAsync(handle, Arg.Any<CancellationToken>()).Returns(
            new JobUpdate(JobState.Succeeded, Result: new ImageGenerationResult([1, 2], "image/png", null)));
        _providers.GetQueuedImageProvider("fal").Returns((IQueuedImageProvider)connection);
        _files.SaveAsync(_session.Id, generation.Id, Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>())
            .Returns("images/fox.png");

        await _service.RecoverInterruptedAsync(ct);
        await _service.WhenIdleAsync();

        Assert.Equal(GenerationStatus.Succeeded, generation.Status);
        Assert.Equal("images/fox.png", generation.ImagePath);
        await queued.DidNotReceive().SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ErrorKind.InvalidKey, GenerationStatus.Failed)]
    [InlineData(ErrorKind.Moderated, GenerationStatus.Filtered)]
    public async Task WatchQueuedAsync_ProviderError_PersistsTerminalOnce(ErrorKind kind, GenerationStatus expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var generation = ResumableGeneration();
        var queued = QueuedProviderThatFails(new ProviderException(kind, "provider says no"));
        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([generation]);

        await _service.RecoverInterruptedAsync(ct);
        await _service.WhenIdleAsync();

        Assert.Equal(expected, generation.Status);
        Assert.Equal(kind, generation.ErrorKind);
        Assert.Equal("provider says no", generation.ErrorMessage);
        Assert.Equal(Now, generation.CompletedAt);
        Assert.Equal(1, _events.Count(e => e.Status == expected));
        await queued.DidNotReceive().SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());

        // The operational entry carries the kind, not the text of the provider; the detail goes to Debug only.
        var operational = _logger.Entries.Where(entry => entry.Level >= LogLevel.Warning).ToList();
        Assert.NotEmpty(operational);
        Assert.All(operational, entry => Assert.DoesNotContain("provider says no", entry.Message, StringComparison.Ordinal));
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("provider says no", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WatchQueuedAsync_UnexpectedPollError_PersistsFailedUnknown()
    {
        var ct = TestContext.Current.CancellationToken;
        var generation = ResumableGeneration();
        var queued = QueuedProviderThatFails(new InvalidTimeZoneException("something odd"));
        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([generation]);

        await _service.RecoverInterruptedAsync(ct);
        await _service.WhenIdleAsync();

        Assert.Equal(GenerationStatus.Failed, generation.Status);
        Assert.Equal(ErrorKind.Unknown, generation.ErrorKind);
        Assert.Equal(Now, generation.CompletedAt);
        Assert.Equal(1, _events.Count(e => e.Status == GenerationStatus.Failed));
    }

    [Fact]
    public async Task WatchQueuedAsync_FileStoreError_PersistsImageSaveFailureNotPollingFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var generation = ResumableGeneration();
        var handle = Handle();
        var connection = Substitute.For<IProviderConnection, IQueuedImageProvider>();
        var queued = (IQueuedImageProvider)connection;
        queued.PollAsync(handle, Arg.Any<CancellationToken>()).Returns(
            new JobUpdate(JobState.Succeeded, Result: new ImageGenerationResult([1, 2], "image/png", null)));
        _providers.GetQueuedImageProvider("fal").Returns((IQueuedImageProvider)connection);

        // The file store fails only after the test has taken the running task, so the failure is seen for sure.
        var gate = new TaskCompletionSource();
        _files.SaveAsync(_session.Id, generation.Id, Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await gate.Task;
                return await Task.FromException<string>(new IOException("disk is full"));
            });
        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([generation]);

        await _service.RecoverInterruptedAsync(ct);
        var idle = _service.WhenIdleAsync();
        gate.SetResult();

        // File persistence failure is converted to one safe terminal result, not a polling failure.
        await idle;
        Assert.Equal(GenerationStatus.Failed, generation.Status);
        Assert.Equal(ErrorKind.ImageSaveFailed, generation.ErrorKind);
        Assert.Equal("Could not save the generated image. Check free space and folder permissions.", generation.ErrorMessage);
        Assert.Single(_events, e => e.Status == GenerationStatus.Failed);
        Assert.DoesNotContain(_logger.Entries, entry => entry.Message.Contains("Polling of generation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WatchQueuedAsync_MalformedHandle_PersistsFailedUnknown()
    {
        var ct = TestContext.Current.CancellationToken;
        var generation = ResumableGeneration();
        generation.JobJson = "{not json";
        var queued = QueuedProviderThatFails(new ProviderException(ErrorKind.InvalidKey, "unused"));
        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([generation]);

        await _service.RecoverInterruptedAsync(ct);
        await _service.WhenIdleAsync();

        Assert.Equal(GenerationStatus.Failed, generation.Status);
        Assert.Equal(ErrorKind.Unknown, generation.ErrorKind);
        Assert.Equal(Now, generation.CompletedAt);
        Assert.Equal(1, _events.Count(e => e.Status == GenerationStatus.Failed));
        await queued.DidNotReceive().PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverInterruptedAsync_MissingProvider_PersistsModelUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var generation = ResumableGeneration();
        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([generation]);
        _providers.GetQueuedImageProvider("fal").Returns(_ => throw new ProviderException(ErrorKind.ModelUnavailable, "no provider configured"));

        await _service.RecoverInterruptedAsync(ct);
        await _service.WhenIdleAsync();

        Assert.Equal(GenerationStatus.Failed, generation.Status);
        Assert.Equal(ErrorKind.ModelUnavailable, generation.ErrorKind);
        Assert.Equal(Now, generation.CompletedAt);
        Assert.Equal(1, _events.Count(e => e.Status == GenerationStatus.Failed));
    }

    [Fact]
    public async Task RecoverInterruptedAsync_MissingProviderAndInterrupted_CountsBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        var missing = ResumableGeneration();
        var resumed = ResumableGeneration("fal2");
        _runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([missing, resumed]);
        _providers.GetQueuedImageProvider("fal").Returns(_ => throw new ProviderException(ErrorKind.ModelUnavailable, "no provider configured"));
        var connection = Substitute.For<IProviderConnection, IQueuedImageProvider>();
        var queued = (IQueuedImageProvider)connection;
        queued.PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>()).Returns(
            new JobUpdate(JobState.Succeeded, Result: new ImageGenerationResult([1, 2], "image/png", null)));
        _providers.GetQueuedImageProvider("fal2").Returns((IQueuedImageProvider)connection);
        _files.SaveAsync(_session.Id, resumed.Id, Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>())
            .Returns("images/fox.png");
        _runs.MarkUnfinishedAsInterruptedAsync(Now, Arg.Any<CancellationToken>()).Returns(2);

        var count = await _service.RecoverInterruptedAsync(ct);
        await _service.WhenIdleAsync();

        // One row finished by the missing provider, two by the bulk update; the resumed job counts as neither.
        Assert.Equal(3, count);
    }

    private ProviderJobHandle Handle() => new("fal", "req-1", "https://q/s", "https://q/r", "https://q/c");

    private Generation ResumableGeneration(string providerId = "fal") => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        SessionId = _session.Id,
        ProviderId = providerId,
        ModelId = "fal-ai/flux/dev",
        Prompt = "fox",
        Seed = 1,
        SeedApplied = true,
        Status = GenerationStatus.Running,
        CreatedAt = Now,
        SortOrder = 0,
        JobJson = System.Text.Json.JsonSerializer.Serialize(Handle()),
    };

    private IQueuedImageProvider QueuedProviderThatFails(Exception failure)
    {
        var connection = Substitute.For<IProviderConnection, IQueuedImageProvider>();
        var queued = (IQueuedImageProvider)connection;
        queued.PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>())
            .Returns<Task<JobUpdate>>(_ => throw failure);
        _providers.GetQueuedImageProvider("fal").Returns((IQueuedImageProvider)connection);
        return queued;
    }

    private sealed class RecordingAsyncDelay : IAsyncDelay
    {
        private readonly ConcurrentQueue<TimeSpan> _delays = new();
        private TaskCompletionSource? _waitForCancellation;

        public IReadOnlyList<TimeSpan> Delays => _delays.ToArray();

        public void WaitForCancellation(TaskCompletionSource entered) => _waitForCancellation = entered;

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            _delays.Enqueue(delay);
            cancellationToken.ThrowIfCancellationRequested();
            if (_waitForCancellation is not null)
            {
                _waitForCancellation.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
        }
    }
}
