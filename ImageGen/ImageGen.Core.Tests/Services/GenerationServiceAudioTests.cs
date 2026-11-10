using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public sealed class GenerationServiceAudioTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public async Task StartAudioRunAsync_TwoModels_CreatesAudioGenerationsWithoutAppliedSeeds()
    {
        var fixture = new Fixture();

        var run = await fixture.Service.StartAudioRunAsync(
            fixture.Session.Id,
            "  ambient music  ",
            [new ModelRef("openrouter", "model-a"), new ModelRef("openrouter", "model-b")],
            TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        Assert.Equal("ambient music", run.Prompt);
        Assert.Equal(["model-a", "model-b"], fixture.Generations.Select(item => item.ModelId));
        Assert.All(fixture.Generations, generation =>
        {
            Assert.Equal(MediaKind.Audio, generation.Kind);
            Assert.False(generation.SeedApplied);
        });
    }

    [Fact]
    public async Task StartAudioRunAsync_ProviderSucceeds_SavesAudioAndPersistsRunParameters()
    {
        var fixture = new Fixture();
        var request = new AudioGenerationRequest(fixture.AudioModel, "music", "alloy");
        fixture.AudioProvider.GenerateAudioAsync(Arg.Any<AudioGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AudioGenerationResponse(new MemoryStream([1, 2, 3]), "audio/mpeg"));
        fixture.Files.SaveMediaStreamAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), "audio/mpeg", ProviderResponseLimits.Audio, Arg.Any<CancellationToken>())
            .Returns("audio/session/track.mp3");
        fixture.Session.Settings.AudioParameters = new AudioParameters { Voice = "alloy" };

        var run = await fixture.Service.StartAudioRunAsync(
            fixture.Session.Id, "music", [new ModelRef("openrouter", "model-a")], TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        var generation = Assert.Single(fixture.Generations);
        Assert.Equal(GenerationStatus.Succeeded, generation.Status);
        Assert.Equal("audio/session/track.mp3", generation.ImagePath);
        Assert.Equal("audio/mpeg", generation.MediaType);
        Assert.Equal("alloy", run.AudioParameters!.Voice);
        await fixture.Files.Received(1).SaveMediaStreamAsync(
            fixture.Session.Id, generation.Id, Arg.Any<Stream>(), "audio/mpeg", ProviderResponseLimits.Audio, Arg.Any<CancellationToken>());
        await fixture.AudioProvider.Received(1).GenerateAudioAsync(
            Arg.Is<AudioGenerationRequest>(value => value.Model == fixture.AudioModel && value.Voice == "alloy"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAudioRunAsync_ProviderFails_MarksGenerationFailed()
    {
        var fixture = new Fixture();
        fixture.AudioProvider.GenerateAudioAsync(Arg.Any<AudioGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AudioGenerationResponse>>(_ => throw new ProviderException(ErrorKind.Network, "offline"));

        await fixture.Service.StartAudioRunAsync(
            fixture.Session.Id, "music", [new ModelRef("openrouter", "model-a")], TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        var generation = Assert.Single(fixture.Generations);
        Assert.Equal(GenerationStatus.Failed, generation.Status);
        Assert.Equal(ErrorKind.Network, generation.ErrorKind);
        Assert.Null(generation.ImagePath);
    }

    [Fact]
    public async Task StartAudioRunAsync_SaveFails_MarksGenerationFailedWithoutPath()
    {
        var fixture = new Fixture();
        fixture.AudioProvider.GenerateAudioAsync(Arg.Any<AudioGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AudioGenerationResponse(new MemoryStream([1, 2, 3]), "audio/mpeg"));
        fixture.Files.SaveMediaStreamAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new IOException("disk full"));

        await fixture.Service.StartAudioRunAsync(
            fixture.Session.Id, "music", [new ModelRef("openrouter", "model-a")], TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        var generation = Assert.Single(fixture.Generations);
        Assert.Equal(GenerationStatus.Failed, generation.Status);
        Assert.Null(generation.ImagePath);
        Assert.Null(generation.MediaType);
    }

    [Fact]
    public async Task NewSeedAsync_AudioGeneration_ThrowsWithoutCreatingRun()
    {
        var fixture = new Fixture();
        var sourceRun = new Run
        {
            Id = Guid.NewGuid(),
            SessionId = fixture.Session.Id,
            Prompt = "music",
            Origin = RunOrigin.Assistant,
            CreatedAt = Now,
            AudioParameters = AudioParameters.Default,
        };
        var source = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = sourceRun.Id,
            SessionId = fixture.Session.Id,
            ProviderId = "openrouter",
            ModelId = "model-a",
            Prompt = "music",
            Seed = 1,
            SeedApplied = false,
            Status = GenerationStatus.Succeeded,
            Kind = MediaKind.Audio,
            CreatedAt = Now,
            SortOrder = 0,
        };
        fixture.Runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        fixture.Runs.GetRunAsync(sourceRun.Id, Arg.Any<CancellationToken>()).Returns(sourceRun);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.NewSeedAsync(source.Id, TestContext.Current.CancellationToken));

        Assert.Empty(fixture.CreatedRuns);
    }

    [Fact]
    public async Task RunOnModelAsync_AudioGeneration_ThrowsWithoutCreatingRun()
    {
        var fixture = new Fixture();
        var (sourceRun, source) = Source(fixture.Session.Id);
        fixture.Runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        fixture.Runs.GetRunAsync(sourceRun.Id, Arg.Any<CancellationToken>()).Returns(sourceRun);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RunOnModelAsync(
            source.Id, new ModelRef("openrouter", "other"), TestContext.Current.CancellationToken));

        Assert.Empty(fixture.CreatedRuns);
    }

    [Fact]
    public async Task RepeatAndEditAsync_AudioGeneration_KeepModelAndVoice()
    {
        var fixture = new Fixture();
        var (sourceRun, source) = Source(fixture.Session.Id);
        fixture.Runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        fixture.Runs.GetRunAsync(sourceRun.Id, Arg.Any<CancellationToken>()).Returns(sourceRun);

        var repeated = await fixture.Service.RepeatAsync(source.Id, TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();
        var edited = await fixture.Service.EditAsync(source.Id, "updated music", TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        Assert.Equal(RunOrigin.Repeat, repeated.Origin);
        Assert.Equal(RunOrigin.Edit, edited.Origin);
        Assert.Equal("updated music", edited.Prompt);
        Assert.Equal(sourceRun.AudioParameters, repeated.AudioParameters);
        Assert.Equal(sourceRun.AudioParameters, edited.AudioParameters);
        Assert.All(fixture.Generations, generation => Assert.Equal("model-a", generation.ModelId));
    }

    [Fact]
    public async Task CancelGenerationAsync_AudioRequest_ReturnsStoppedWaitingAndMarksCancelled()
    {
        var fixture = new Fixture();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.AudioProvider.GenerateAudioAsync(Arg.Any<AudioGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                return new AudioGenerationResponse(new MemoryStream(), "audio/mpeg");
            });
        await fixture.Service.StartAudioRunAsync(
            fixture.Session.Id, "music", [new ModelRef("openrouter", "model-a")], TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var generation = Assert.Single(fixture.Generations);

        var outcome = await fixture.Service.CancelGenerationAsync(generation.Id, TestContext.Current.CancellationToken);
        await fixture.Service.WhenIdleAsync();

        Assert.Equal(GenerationCancelResult.StoppedWaiting, outcome);
        Assert.Equal(GenerationStatus.Cancelled, generation.Status);
    }

    private static (Run Run, Generation Generation) Source(Guid sessionId)
    {
        var run = new Run
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Prompt = "music",
            Origin = RunOrigin.Assistant,
            CreatedAt = Now,
            AudioParameters = new AudioParameters { Voice = "alloy" },
        };
        var generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            SessionId = sessionId,
            ProviderId = "openrouter",
            ModelId = "model-a",
            Prompt = "music",
            Seed = 1,
            SeedApplied = false,
            Status = GenerationStatus.Succeeded,
            Kind = MediaKind.Audio,
            CreatedAt = Now,
            SortOrder = 0,
        };
        return (run, generation);
    }

    private sealed class Fixture
    {
        public ISessionRepository Sessions { get; } = Substitute.For<ISessionRepository>();
        public IRunRepository Runs { get; } = Substitute.For<IRunRepository>();
        public IProviderRouter Providers { get; } = Substitute.For<IProviderRouter>();
        public IImageFileStore Files { get; } = Substitute.For<IImageFileStore>();
        public IImageModelRepository ImageModels { get; } = Substitute.For<IImageModelRepository>();
        public IAudioModelRepository AudioModels { get; } = Substitute.For<IAudioModelRepository>();
        public IChatAttachmentRepository Attachments { get; } = Substitute.For<IChatAttachmentRepository>();
        public ISeedSource Seeds { get; } = Substitute.For<ISeedSource>();
        public IAudioGenerationProvider AudioProvider { get; } = Substitute.For<IAudioGenerationProvider>();
        public AudioModel AudioModel { get; } = new()
        {
            ProviderId = "openrouter",
            Id = "model-a",
            Name = "Model A",
            Output = AudioOutput.ChatAudio,
            FetchedAt = Now,
        };
        public List<Generation> Generations { get; } = [];
        public List<Run> CreatedRuns { get; } = [];
        public Session Session { get; } = new()
        {
            Id = Guid.NewGuid(),
            Title = "Audio",
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
            Runs.AddRunAsync(Arg.Do<Run>(CreatedRuns.Add), Arg.Do<IReadOnlyList<Generation>>(items => Generations.AddRange(items)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            Runs.GetRunAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call =>
                Task.FromResult(CreatedRuns.SingleOrDefault(run => run.Id == call.Arg<Guid>())));
            Runs.GetGenerationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call =>
                Task.FromResult(Generations.SingleOrDefault(generation => generation.Id == call.Arg<Guid>())));
            Runs.UpdateGenerationAsync(Arg.Any<Generation>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            Runs.ListResumableJobsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Generation>());
            Seeds.NextSeed().Returns(123L);
            Providers.GetAudioGenerationProvider("openrouter").Returns(AudioProvider);
            AudioModels.ListAsync("openrouter", Arg.Any<CancellationToken>()).Returns(call =>
                Task.FromResult<IReadOnlyList<AudioModel>>([AudioModel]));
            var catalog = new ModelCatalogService(Providers, ImageModels, NullLogger<ModelCatalogService>.Instance);
            var audioCatalog = new AudioModelCatalogService(Providers, AudioModels, NullLogger<AudioModelCatalogService>.Instance);
            var attachmentService = new AttachmentService(Attachments, Files, time);
            Service = new GenerationService(
                Sessions,
                Runs,
                Providers,
                Files,
                catalog,
                Seeds,
                attachmentService,
                time,
                NullLogger<GenerationService>.Instance,
                new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance),
                audioCatalog: audioCatalog);
        }
    }
}
