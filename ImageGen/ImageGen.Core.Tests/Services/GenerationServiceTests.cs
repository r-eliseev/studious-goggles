using System.Collections.Concurrent;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
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
    private readonly IModelProvider _generator = Substitute.For<IModelProvider>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly ISeedSource _seeds = Substitute.For<ISeedSource>();
    private readonly IImageModelRepository _modelRepository = Substitute.For<IImageModelRepository>();
    private readonly ConcurrentQueue<Generation> _events = new();
    private readonly CapturingLogger<GenerationService> _logger = new();
    private readonly IChatAttachmentRepository _chatAttachments = Substitute.For<IChatAttachmentRepository>();
    private readonly AttachmentService _attachments;
    private readonly GenerationService _service;
    private readonly Session _session;
    private IReadOnlyList<Generation> _added = [];
    private readonly ConcurrentDictionary<Guid, Run> _savedRuns = new();

    public GenerationServiceTests()
    {
        _providers.GetImageProvider(ProviderIds.OpenRouter).Returns(_generator);
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
            _sessions, _runs, _providers, _files, catalog, _seeds, _attachments, time, _logger);
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
            _sessions, _runs, _providers, _files, catalog, _seeds, _attachments, time, _logger, trace);

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
        Assert.Contains("is Queued", messages[0], StringComparison.Ordinal);
        Assert.Contains("is Running", messages[1], StringComparison.Ordinal);
        Assert.Contains("is Succeeded", messages[2], StringComparison.Ordinal);
        Assert.Equal(3, messages.Count);
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
        _providers.GetConnection("fal").Returns(connection);
        _files.SaveAsync(_session.Id, generation.Id, Arg.Any<byte[]>(), "image/png", Arg.Any<CancellationToken>())
            .Returns("images/fox.png");

        await _service.RecoverInterruptedAsync(ct);
        await _service.WhenIdleAsync();

        Assert.Equal(GenerationStatus.Succeeded, generation.Status);
        Assert.Equal("images/fox.png", generation.ImagePath);
        await queued.DidNotReceive().SubmitAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>());
    }
}
