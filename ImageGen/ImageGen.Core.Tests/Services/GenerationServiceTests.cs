using System.Collections.Concurrent;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
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
    private readonly IModelProvider _generator = Substitute.For<IModelProvider>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly ISeedSource _seeds = Substitute.For<ISeedSource>();
    private readonly IImageModelRepository _modelRepository = Substitute.For<IImageModelRepository>();
    private readonly ConcurrentQueue<Generation> _events = new();
    private readonly GenerationService _service;
    private readonly Session _session;
    private IReadOnlyList<Generation> _added = [];

    public GenerationServiceTests()
    {
        _providers.GetImageProvider(ProviderIds.OpenRouter).Returns(_generator);
        _providers.ImageProviderIds.Returns([ProviderIds.OpenRouter]);
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [
                ModelCatalogServiceTests.Model(Seeded, "Seeded"),
                ModelCatalogServiceTests.Model(Unseeded, "Unseeded", supportsSeed: false),
            ]);
        var catalog = new ModelCatalogService(_providers, _modelRepository);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);

        _seeds.NextSeed().Returns(11L, 22L, 33L);
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ImageGenerationResult(Image, "image/png", 0.014m));
        _files.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("images/session/generation.png");
        _runs.AddRunAsync(Arg.Any<Run>(), Arg.Do<IReadOnlyList<Generation>>(g => _added = g), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

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

        _service = new GenerationService(
            _sessions, _runs, _providers, _files, catalog, _seeds, time, NullLogger<GenerationService>.Instance);
        _service.GenerationUpdated += (_, e) => _events.Enqueue(e.Generation);
    }

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
        _providers.GetImageProvider("fal").Returns<IModelProvider>(_ => throw new ProviderException(ErrorKind.ModelUnavailable, "Provider 'fal' is not configured."));

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
}
