using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Tests.PageModels;

public class ModelPickerPageModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.Parse("5b0f5c1e-3a57-4c59-9d2b-1f0c7d3e8a11");

    private static ModelRef Ref(string modelId) => new(ProviderIds.OpenRouter, modelId);

    private static readonly ImageModel Alpha = TestModels.Model("vendor/alpha", "Alpha");
    private static readonly ImageModel Beta = TestModels.Model("vendor/beta", "Beta Pro", supportsSeed: false);
    private static readonly ImageModel Gamma = TestModels.Model("other/gamma", "Gamma");

    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly IModelProvider _provider = Substitute.For<IModelProvider>();
    private readonly ISynchronousImageTransport _transport = Substitute.For<ISynchronousImageTransport>();
    private readonly IProviderRouter _providers;
    private readonly IImageModelRepository _modelRepository = Substitute.For<IImageModelRepository>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly ISeedSource _seeds = Substitute.For<ISeedSource>();
    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IChatAttachmentRepository _chatAttachments = Substitute.For<IChatAttachmentRepository>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ModelCatalogService _catalog;
    private readonly GenerationService _generations;
    private readonly Generation _source;
    private IReadOnlyList<Generation> _added = [];

    public ModelPickerPageModelTests()
    {
        _providers = ProviderDoubles.Router(_provider, transport: _transport);
        _modelRepository.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([Alpha, Beta, Gamma]);
        _provider.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns([Alpha, Beta, Gamma]);
        _transport.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ImageGenerationResult([1, 2, 3], "image/png", 0.01m));
        _files.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("images/x.png");
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _catalog = new ModelCatalogService(_providers, _modelRepository, NullLogger<ModelCatalogService>.Instance);
        var attachments = new AttachmentService(_chatAttachments, _files, time);
        _generations = new GenerationService(
            _sessions, _runs, _providers, _files, _catalog, _seeds, attachments, time, NullLogger<GenerationService>.Instance, new SessionChangeNotifier());

        _source = Generation("vendor/alpha", seed: 33);
        _runs.GetGenerationAsync(_source.Id, Arg.Any<CancellationToken>()).Returns(_source);
        _runs.GetRunAsync(_source.RunId, Arg.Any<CancellationToken>()).Returns(new Run
        {
            Id = _source.RunId,
            SessionId = SessionId,
            Prompt = _source.Prompt,
            Origin = RunOrigin.Assistant,
            CreatedAt = Now,
        });
        _runs.AddRunAsync(Arg.Any<Run>(), Arg.Do<IReadOnlyList<Generation>>(g => _added = g), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private static Generation Generation(string modelId, long seed, string prompt = "a red fox", int sortOrder = 0) => new()
    {
        Id = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        SessionId = SessionId,
        ProviderId = ProviderIds.OpenRouter,
        ModelId = modelId,
        Prompt = prompt,
        Seed = seed,
        SeedApplied = true,
        Status = GenerationStatus.Failed,
        CreatedAt = Now,
        SortOrder = sortOrder,
    };

    private ModelPickerPageModel CreateModel()
    {
        var model = new ModelPickerPageModel(_runs, _catalog, _generations, ProviderDoubles.BuiltInRegistry(), _navigation, _dialogs);
        model.ApplyQueryAttributes(new Dictionary<string, object>
        {
            [Routes.SessionIdParameter] = SessionId.ToString("D"),
            [Routes.GenerationIdParameter] = _source.Id.ToString("D"),
        });
        return model;
    }

    private async Task<ModelPickerPageModel> OpenAsync()
    {
        var model = CreateModel();
        await model.OnAppearingAsync();
        return model;
    }

    [Fact]
    public async Task Load_UsedModels_ExcludeSourceModel()
    {
        _runs.ListGenerationsBySessionAsync(SessionId, Arg.Any<CancellationToken>()).Returns(new[]
        {
            Generation("vendor/alpha", seed: 1, sortOrder: 0),
            Generation("vendor/beta", seed: 2, sortOrder: 1),
        });

        var model = await OpenAsync();

        Assert.Equal(new[] { "vendor/beta" }, model.UsedModels.Select(m => m.Id));
    }

    [Fact]
    public async Task Load_UsedModels_OrderedByLastUse()
    {
        _runs.ListGenerationsBySessionAsync(SessionId, Arg.Any<CancellationToken>()).Returns(new[]
        {
            Generation("vendor/beta", seed: 1),
            Generation("other/gamma", seed: 2),
            Generation("vendor/beta", seed: 3),
        });

        var model = await OpenAsync();

        // Beta was used last (reused), so it comes before Gamma; the source model Alpha is excluded.
        Assert.Equal(new[] { "vendor/beta", "other/gamma" }, model.UsedModels.Select(m => m.Id));
    }

    [Fact]
    public async Task Search_FiltersAllModelsOnly()
    {
        _runs.ListGenerationsBySessionAsync(SessionId, Arg.Any<CancellationToken>()).Returns(new[]
        {
            Generation("vendor/beta", seed: 1),
            Generation("other/gamma", seed: 2),
        });
        var model = await OpenAsync();
        Assert.Equal(3, model.AllModels.Count);
        var usedBefore = model.UsedModels.Select(m => m.Id).ToArray();

        model.SearchText = "beta";

        Assert.Equal(new[] { "vendor/beta" }, model.AllModels.Select(m => m.Id));
        Assert.Equal(usedBefore, model.UsedModels.Select(m => m.Id));
    }

    [Fact]
    public async Task Pick_StartsRunAndGoesBack()
    {
        _runs.ListGenerationsBySessionAsync(SessionId, Arg.Any<CancellationToken>()).Returns(System.Array.Empty<Generation>());
        var model = await OpenAsync();
        var chosen = model.AllModels.Single(m => m.Id == "vendor/beta");

        await model.PickCommand.ExecuteAsync(chosen);
        await _generations.WhenIdleAsync();

        Assert.Equal([("vendor/beta", 33L, "a red fox")], _added.Select(g => (g.ModelId, g.Seed, g.Prompt)));
        await _navigation.Received(1).GoBackAsync();
    }
}
