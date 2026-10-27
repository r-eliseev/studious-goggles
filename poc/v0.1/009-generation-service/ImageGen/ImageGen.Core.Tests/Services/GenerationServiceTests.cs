using System.Collections.Concurrent;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public class GenerationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly IImageGenerator _generator = Substitute.For<IImageGenerator>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly IImageModelRepository _models = Substitute.For<IImageModelRepository>();
    private readonly ISeedSource _seeds = Substitute.For<ISeedSource>();
    private readonly ConcurrentBag<Generation> _events = [];
    private readonly GenerationService _service;
    private readonly Session _session;
    private Run? _savedRun;
    private IReadOnlyList<Generation> _savedGenerations = [];

    public GenerationServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _models.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            ModelCatalogServiceTests.Model("seeded", "Seeded", supportsSeed: true),
            ModelCatalogServiceTests.Model("unseeded", "Unseeded", supportsSeed: false),
        ]);
        var catalog = new ModelCatalogService(Substitute.For<IModelProvider>(), _models);
        _session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "s",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = new SessionSettings { ImageModelIds = ["seeded", "unseeded"], RunsPerModel = 2 },
        };
        _sessions.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);
        _runs.AddRunAsync(Arg.Do<Run>(r => _savedRun = r), Arg.Do<IReadOnlyList<Generation>>(g => _savedGenerations = g), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _seeds.NextSeed().Returns(11L, 22L, 33L);
        _files.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("images/x.png");
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ImageGenerationResult([1, 2, 3], "image/png", 0.01m));
        _service = new GenerationService(
            _sessions, _runs, _generator, _files, catalog, _seeds, time, NullLogger<GenerationService>.Instance);
        _service.GenerationUpdated += (_, e) => _events.Add(e.Generation);
    }

    [Fact]
    public async Task StartRunAsync_TwoModelsTwoRuns_CreatesFourGenerationsWithSharedSeeds()
    {
        var run = await _service.StartRunAsync(_session.Id, " a fox ", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Same(run, _savedRun);
        Assert.Equal("a fox", run.Prompt);
        Assert.Equal(RunOrigin.Assistant, run.Origin);
        Assert.Equal(
            [("seeded", 11L), ("seeded", 22L), ("unseeded", 11L), ("unseeded", 22L)],
            _savedGenerations.Select(g => (g.ModelId, g.Seed)));
        Assert.Equal([0, 1, 2, 3], _savedGenerations.Select(g => g.SortOrder));
        Assert.All(_savedGenerations.Where(g => g.ModelId == "seeded"), g => Assert.True(g.SeedApplied));
        Assert.All(_savedGenerations.Where(g => g.ModelId == "unseeded"), g => Assert.False(g.SeedApplied));
    }

    [Fact]
    public async Task StartRunAsync_Success_SavesFileThenMarksSucceeded()
    {
        await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.All(_savedGenerations, g =>
        {
            Assert.Equal(GenerationStatus.Succeeded, g.Status);
            Assert.Equal("images/x.png", g.ImagePath);
            Assert.Equal("image/png", g.MediaType);
            Assert.Equal(0.01m, g.CostUsd);
            Assert.Equal(Now, g.CompletedAt);
        });
        Assert.Equal(4, _events.Count(e => e.Status == GenerationStatus.Running));
        Assert.Equal(4, _events.Count(e => e.Status == GenerationStatus.Succeeded));
    }

    [Fact]
    public async Task StartRunAsync_SeedOnlyForSeededModel_IsSentToGenerator()
    {
        await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        await _generator.Received(2).GenerateAsync(
            Arg.Is<ImageGenerationRequest>(r => r.ModelId == "seeded" && r.Seed != null), Arg.Any<CancellationToken>());
        await _generator.Received(2).GenerateAsync(
            Arg.Is<ImageGenerationRequest>(r => r.ModelId == "unseeded" && r.Seed == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartRunAsync_ModeratedError_MarksFiltered()
    {
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ImageGenerationResult>(new ProviderException(ErrorKind.Moderated, "flagged")));

        await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.All(_savedGenerations, g =>
        {
            Assert.Equal(GenerationStatus.Filtered, g.Status);
            Assert.Equal(ErrorKind.Moderated, g.ErrorKind);
        });
    }

    [Fact]
    public async Task StartRunAsync_OneModelFails_OthersSucceed()
    {
        _generator.GenerateAsync(Arg.Is<ImageGenerationRequest>(r => r.ModelId == "unseeded"), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ImageGenerationResult>(new ProviderException(ErrorKind.RateLimited, "slow down")));

        await _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.All(_savedGenerations.Where(g => g.ModelId == "seeded"), g => Assert.Equal(GenerationStatus.Succeeded, g.Status));
        Assert.All(_savedGenerations.Where(g => g.ModelId == "unseeded"), g =>
        {
            Assert.Equal(GenerationStatus.Failed, g.Status);
            Assert.Equal(ErrorKind.RateLimited, g.ErrorKind);
            Assert.Equal("slow down", g.ErrorMessage);
        });
    }

    [Fact]
    public async Task StartRunAsync_NoModels_ThrowsInvalidOperation()
    {
        _session.Settings.ImageModelIds = [];

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartRunAsync(_session.Id, "a fox", TestContext.Current.CancellationToken));
    }

    private Generation Source() => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        SessionId = _session.Id,
        ModelId = "seeded",
        Prompt = "old prompt",
        Seed = 777,
        SeedApplied = true,
        Status = GenerationStatus.Succeeded,
        CreatedAt = Now,
        SortOrder = 0,
    };

    [Fact]
    public async Task RepeatAsync_UsesSamePromptModelAndSeed()
    {
        var source = Source();
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var run = await _service.RepeatAsync(source.Id, TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal(RunOrigin.Repeat, run.Origin);
        var generation = Assert.Single(_savedGenerations);
        Assert.Equal(("seeded", "old prompt", 777L), (generation.ModelId, generation.Prompt, generation.Seed));
    }

    [Fact]
    public async Task NewSeedAsync_UsesNewSeed()
    {
        var source = Source();
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var run = await _service.NewSeedAsync(source.Id, TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal(RunOrigin.NewSeed, run.Origin);
        var generation = Assert.Single(_savedGenerations);
        Assert.Equal(("seeded", "old prompt", 11L), (generation.ModelId, generation.Prompt, generation.Seed));
    }

    [Fact]
    public async Task EditAsync_UsesNewPromptSameModelAndSeed()
    {
        var source = Source();
        _runs.GetGenerationAsync(source.Id, Arg.Any<CancellationToken>()).Returns(source);

        var run = await _service.EditAsync(source.Id, " new prompt ", TestContext.Current.CancellationToken);
        await _service.WhenIdleAsync();

        Assert.Equal(RunOrigin.Edit, run.Origin);
        Assert.Equal("new prompt", run.Prompt);
        var generation = Assert.Single(_savedGenerations);
        Assert.Equal(("seeded", "new prompt", 777L), (generation.ModelId, generation.Prompt, generation.Seed));
    }

    [Fact]
    public async Task RecoverInterruptedAsync_CallsRepositoryWithNow()
    {
        _runs.MarkUnfinishedAsInterruptedAsync(Now, Arg.Any<CancellationToken>()).Returns(3);

        Assert.Equal(3, await _service.RecoverInterruptedAsync(TestContext.Current.CancellationToken));
    }
}
