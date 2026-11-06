using System.Collections.Concurrent;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public sealed class GenerationServiceVideoTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartVideoRunAsync_TwoModels_SavesVideoRunAndOneGenerationPerModel()
    {
        var fixture = new Fixture();

        var run = await fixture.Service.StartVideoRunAsync(
            fixture.Session.Id,
            "  a short film  ",
            [new ModelRef("openrouter", "seeded"), new ModelRef("openrouter", "plain")],
            new VideoParameters { DurationSeconds = 5 },
            null,
            TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        Assert.Equal("a short film", run.Prompt);
        Assert.Equal(5, run.VideoParameters!.DurationSeconds);
        Assert.Null(run.FirstFrameGenerationId);
        Assert.Equal(["seeded", "plain"], fixture.Generations.Select(g => g.ModelId));
        Assert.All(fixture.Generations, generation =>
        {
            Assert.Equal(MediaKind.Video, generation.Kind);
            Assert.Equal(5, generation.DurationSeconds);
        });
        Assert.True(fixture.Generations[0].SeedApplied);
        Assert.False(fixture.Generations[1].SeedApplied);
    }

    [Fact]
    public async Task StartVideoRunAsync_NoModels_ThrowsWithoutCreatingRun()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.StartVideoRunAsync(
            fixture.Session.Id, "film", [], VideoParameters.Default, null, TestContext.Current.CancellationToken));

        await fixture.Runs.DidNotReceive().AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartVideoRunAsync_ProviderSucceeds_SavesStreamAndPublishesTerminalState()
    {
        var fixture = new Fixture();
        var poll = 0;
        fixture.VideoProvider.PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Interlocked.Increment(ref poll) == 1
                ? new VideoJobUpdate(JobState.Running)
                : new VideoJobUpdate(JobState.Succeeded, 0.4m)));
        fixture.Files.SaveMediaStreamAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), "video/mp4", ProviderResponseLimits.Video, Arg.Any<CancellationToken>())
            .Returns("videos/session/result.mp4");

        await fixture.Service.StartVideoRunAsync(
            fixture.Session.Id, "a film", [new ModelRef("openrouter", "seeded")], VideoParameters.Default, null,
            TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        var generation = Assert.Single(fixture.Generations);
        Assert.Equal(GenerationStatus.Succeeded, generation.Status);
        Assert.Equal(0.4m, generation.CostUsd);
        Assert.Equal("videos/session/result.mp4", generation.ImagePath);
        Assert.Equal("video/mp4", generation.MediaType);
        Assert.Contains(fixture.Events, e => e.Status == GenerationStatus.Running);
        Assert.Contains(fixture.Events, e => e.Status == GenerationStatus.Succeeded);
        Assert.Equal(TimeSpan.FromSeconds(5), fixture.Delay.Delays[0]);
        Assert.All(fixture.Delay.Delays, delay => Assert.InRange(delay, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)));
        await fixture.Files.Received(1).SaveMediaStreamAsync(
            fixture.Session.Id, generation.Id, Arg.Any<Stream>(), "video/mp4", ProviderResponseLimits.Video, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartVideoRunAsync_SaveMediaStreamFails_MarksFailedAndKeepsProviderCost()
    {
        var fixture = new Fixture();
        fixture.VideoProvider.PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>())
            .Returns(new VideoJobUpdate(JobState.Succeeded, 0.4m));
        fixture.Files.SaveMediaStreamAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new ProviderException(ErrorKind.Unknown, "video too large"));

        await fixture.Service.StartVideoRunAsync(
            fixture.Session.Id, "film", [new ModelRef("openrouter", "seeded")], VideoParameters.Default, null,
            TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        var generation = Assert.Single(fixture.Generations);
        Assert.Equal(GenerationStatus.Failed, generation.Status);
        Assert.Equal(ErrorKind.Unknown, generation.ErrorKind);
        Assert.Equal("video too large", generation.ErrorMessage);
        Assert.Equal(0.4m, generation.CostUsd);
        Assert.Null(generation.ImagePath);
    }

    [Fact]
    public async Task StartVideoRunAsync_SubmitSlotIsReleasedBeforePollingCompletes()
    {
        var fixture = new Fixture();
        var submissions = 0;
        var polls = 0;
        var allPolling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<VideoJobUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.VideoProvider.SubmitAsync(Arg.Any<VideoGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var index = Interlocked.Increment(ref submissions);
            return Task.FromResult(Fixture.Handle with { RequestId = $"video-{index}" });
        });
        fixture.VideoProvider.PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (Interlocked.Increment(ref polls) == 5)
            {
                allPolling.TrySetResult();
            }

            return await finish.Task.WaitAsync(call.Arg<CancellationToken>());
        });
        var models = Enumerable.Range(0, 5).Select(index => new ModelRef("openrouter", $"model-{index}")).ToArray();

        await fixture.Service.StartVideoRunAsync(
            fixture.Session.Id, "film", models, VideoParameters.Default, null, TestContext.Current.CancellationToken);
        await allPolling.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(5, Volatile.Read(ref submissions));
        finish.TrySetResult(new VideoJobUpdate(JobState.Failed, ErrorKind: ErrorKind.Unknown));
        await fixture.Service.WhenIdleAsync();
    }

    [Fact]
    public async Task StartVideoRunAsync_FirstFrameFromAnotherSession_ThrowsBeforeSubmission()
    {
        var fixture = new Fixture();
        var source = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ProviderId = "openrouter",
            ModelId = "still",
            Prompt = "frame",
            Seed = 1,
            SeedApplied = false,
            Status = GenerationStatus.Succeeded,
            Kind = MediaKind.Image,
            ImagePath = "images/frame.png",
            MediaType = "image/png",
            CreatedAt = Now,
            SortOrder = 0,
        };
        fixture.Runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.StartVideoRunAsync(
            fixture.Session.Id, "film", [new ModelRef("openrouter", "seeded")], VideoParameters.Default, source.Id,
            TestContext.Current.CancellationToken));

        await fixture.VideoProvider.DidNotReceive().SubmitAsync(Arg.Any<VideoGenerationRequest>(), Arg.Any<CancellationToken>());
        await fixture.Runs.DidNotReceive().AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverInterruptedAsync_VideoHandle_ContinuesPollWithoutResubmitting()
    {
        var fixture = new Fixture();
        var generation = fixture.ResumableVideoGeneration();
        generation.JobJson = System.Text.Json.JsonSerializer.Serialize(Fixture.Handle);
        fixture.Runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns([generation]);
        fixture.VideoProvider.PollAsync(Fixture.Handle, Arg.Any<CancellationToken>())
            .Returns(new VideoJobUpdate(JobState.Failed, ErrorKind: ErrorKind.BadRequest, ErrorMessage: "invalid request"));

        await fixture.Service.RecoverInterruptedAsync(TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        Assert.Equal(GenerationStatus.Failed, generation.Status);
        Assert.Equal(ErrorKind.BadRequest, generation.ErrorKind);
        Assert.Equal("invalid request", generation.ErrorMessage);
        await fixture.VideoProvider.DidNotReceive().SubmitAsync(Arg.Any<VideoGenerationRequest>(), Arg.Any<CancellationToken>());
    }

    private sealed class Fixture
    {
        public static ProviderJobHandle Handle { get; } = new("openrouter", "video-1", "https://openrouter.ai/api/v1/videos/video-1", "https://openrouter.ai/api/v1/videos/video-1/content?index=0", string.Empty);

        public ISessionRepository Sessions { get; } = Substitute.For<ISessionRepository>();
        public IRunRepository Runs { get; } = Substitute.For<IRunRepository>();
        public IProviderRouter Providers { get; } = Substitute.For<IProviderRouter>();
        public IImageFileStore Files { get; } = Substitute.For<IImageFileStore>();
        public IVideoModelRepository VideoModels { get; } = Substitute.For<IVideoModelRepository>();
        public IImageModelRepository ImageModels { get; } = Substitute.For<IImageModelRepository>();
        public IChatAttachmentRepository AttachmentsRepository { get; } = Substitute.For<IChatAttachmentRepository>();
        public ISeedSource Seeds { get; } = Substitute.For<ISeedSource>();
        public IQueuedVideoProvider VideoProvider { get; } = Substitute.For<IQueuedVideoProvider, IVideoModelProvider>();
        public RecordingDelay Delay { get; } = new();
        public ConcurrentQueue<Generation> Events { get; } = new();
        public List<Generation> Generations { get; private set; } = [];
        public Session Session { get; } = new()
        {
            Id = Guid.NewGuid(),
            Title = "Video",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = new SessionSettings(),
        };
        public GenerationService Service { get; }

        public Fixture()
        {
            var time = Substitute.For<TimeProvider>();
            time.GetUtcNow().Returns(Now);
            Sessions.GetAsync(Session.Id, Arg.Any<CancellationToken>()).Returns(Session);
            Runs.AddRunAsync(Arg.Do<Run>(_ => { }), Arg.Do<IReadOnlyList<Generation>>(items => Generations = items.ToList()), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            Runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Generation>());
            Runs.MarkUnfinishedAsInterruptedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(0);
            Providers.GetQueuedVideoProvider("openrouter").Returns(VideoProvider);
            Providers.GetVideoModelProvider("openrouter").Returns((IVideoModelProvider)VideoProvider);
            ((IVideoModelProvider)VideoProvider).ListVideoModelsAsync(Arg.Any<CancellationToken>()).Returns(
            [
                new VideoModel { ProviderId = "openrouter", Id = "seeded", Name = "Seeded", Durations = [5], Resolutions = [], AspectRatios = [], FrameImages = [], SupportsSeed = true, PriceSkus = new Dictionary<string, decimal>(), FetchedAt = Now },
                new VideoModel { ProviderId = "openrouter", Id = "plain", Name = "Plain", Durations = [5], Resolutions = [], AspectRatios = [], FrameImages = [], SupportsSeed = false, PriceSkus = new Dictionary<string, decimal>(), FetchedAt = Now },
            ]);
            VideoModels.ListAsync("openrouter", Arg.Any<CancellationToken>()).Returns(call =>
                ((IVideoModelProvider)VideoProvider).ListVideoModelsAsync(call.Arg<CancellationToken>()));
            Seeds.NextSeed().Returns(100L, 200L);
            VideoProvider.SubmitAsync(Arg.Any<VideoGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(Handle);
            VideoProvider.PollAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>())
                .Returns(new VideoJobUpdate(JobState.Failed, ErrorKind: ErrorKind.Unknown, ErrorMessage: "not configured"));
            Files.SaveMediaStreamAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns("videos/session/video.mp4");
            VideoProvider.OpenContentAsync(Arg.Any<ProviderJobHandle>(), Arg.Any<CancellationToken>()).Returns(
                new ProviderContent(new MemoryStream([1, 2, 3]), "video/mp4", new HttpResponseMessage(), new CancellationTokenSource()));
            Files.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([1, 2, 3]);

            var imageCatalog = new ModelCatalogService(Providers, ImageModels, NullLogger<ModelCatalogService>.Instance);
            var videoCatalog = new VideoModelCatalogService(Providers, VideoModels, NullLogger<VideoModelCatalogService>.Instance);
            var attachmentService = new AttachmentService(AttachmentsRepository, Files, time);
            Service = new GenerationService(
                Sessions, Runs, Providers, Files, imageCatalog, Seeds, attachmentService, time,
                NullLogger<GenerationService>.Instance, new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance),
                delay: Delay, videoCatalog: videoCatalog);
            Service.GenerationUpdated += (_, args) => Events.Enqueue(args.Generation);
        }

        public Generation ResumableVideoGeneration() => new()
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = Session.Id,
            ProviderId = "openrouter",
            ModelId = "seeded",
            Prompt = "resume",
            Seed = 100,
            SeedApplied = true,
            Status = GenerationStatus.Running,
            Kind = MediaKind.Video,
            DurationSeconds = 5,
            CreatedAt = Now,
            SortOrder = 0,
        };

        public sealed class RecordingDelay : IAsyncDelay
        {
            private readonly ConcurrentQueue<TimeSpan> _delays = new();

            public IReadOnlyList<TimeSpan> Delays => _delays.ToArray();

            public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
            {
                _delays.Enqueue(delay);
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }
    }
}
