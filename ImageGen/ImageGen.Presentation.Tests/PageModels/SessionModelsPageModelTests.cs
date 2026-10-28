using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Tests.TestDoubles;
using NSubstitute.ExceptionExtensions;

namespace ImageGen.Presentation.Tests.PageModels;

public class SessionModelsPageModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.Parse("5b0f5c1e-3a57-4c59-9d2b-1f0c7d3e8a11");
    private const string AssistantModel = "vendor/assistant";
    private const string RemovedModel = "vendor/removed";

    private static ModelRef Ref(string modelId) => new(ProviderIds.OpenRouter, modelId);

    // Catalog in catalog order (by name). Beta does not support seed.
    private static readonly ImageModel Alpha = TestModels.Model("vendor/alpha", "Alpha");
    private static readonly ImageModel Beta = TestModels.Model("vendor/beta", "Beta Pro", supportsSeed: false);
    private static readonly ImageModel Gamma = TestModels.Model("other/gamma", "Gamma");

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IChatMessageRepository _messages = Substitute.For<IChatMessageRepository>();
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly IModelProvider _provider = Substitute.For<IModelProvider>();
    private readonly IImageModelRepository _modelRepository = Substitute.For<IImageModelRepository>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly Session _session;
    private readonly SessionService _sessionService;
    private readonly ModelCatalogService _catalog;
    private Session? _saved;

    public SessionModelsPageModelTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _sessionService = new SessionService(_sessions, _messages, _runs, _files, time);
        _catalog = new ModelCatalogService(ProviderDoubles.Router(_provider), _modelRepository);

        _session = new Session
        {
            Id = SessionId,
            Title = "Red fox",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = new SessionSettings
            {
                ImageModels = [Ref(Beta.Id), Ref(RemovedModel)],
                RunsPerModel = 3,
                AssistantModel = Ref(AssistantModel),
            },
        };
        _sessions.GetAsync(SessionId, Arg.Any<CancellationToken>()).Returns(_session);
        _sessions.UpdateAsync(Arg.Do<Session>(saved => _saved = saved), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _modelRepository.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([Alpha, Beta, Gamma]);
        _provider.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns([Gamma, Alpha, Beta]);
    }

    private SessionModelsPageModel CreateModel()
    {
        var model = new SessionModelsPageModel(_sessionService, _catalog, _navigation, _dialogs);
        model.ApplyQueryAttributes(new Dictionary<string, object> { [Routes.SessionIdParameter] = SessionId.ToString("D") });
        return model;
    }

    private async Task<SessionModelsPageModel> OpenAsync()
    {
        var model = CreateModel();
        await model.OnAppearingAsync();
        return model;
    }

    private static string[] Ids(IEnumerable<ModelOptionViewModel> models) => models.Select(m => m.Id).ToArray();

    [Fact]
    public async Task OnAppearingAsync_ShowsCatalogWithSelectionAndRunsFromSettings()
    {
        var model = await OpenAsync();

        Assert.Equal(new[] { "vendor/alpha", "vendor/beta", "other/gamma" }, Ids(model.Models));
        Assert.False(model.Models[0].IsSelected);
        Assert.True(model.Models[1].IsSelected);
        Assert.False(model.Models[2].IsSelected);
        Assert.Equal(3, model.RunsPerModel);
        Assert.Equal("Runs per model: 3", model.RunsPerModelText);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task OnAppearingAsync_ReadsCatalogWithoutForcingRefresh()
    {
        await OpenAsync();

        await _modelRepository.Received(1).ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>());
        await _provider.DidNotReceive().ListImageModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchText_FiltersByNameIgnoringCase()
    {
        var model = await OpenAsync();

        model.SearchText = "ALPHA";

        Assert.Equal(new[] { "vendor/alpha" }, Ids(model.Models));
    }

    [Fact]
    public async Task SearchText_FiltersById()
    {
        var model = await OpenAsync();

        model.SearchText = "other/";

        Assert.Equal(new[] { "other/gamma" }, Ids(model.Models));
    }

    [Fact]
    public async Task SearchText_BlankAfterTrim_ShowsAllModels()
    {
        var model = await OpenAsync();
        model.SearchText = "gamma";

        model.SearchText = "   ";

        Assert.Equal(3, model.Models.Count);
    }

    [Fact]
    public async Task SearchText_NoMatch_ShowsNoModels()
    {
        var model = await OpenAsync();

        model.SearchText = "nothing like this";

        Assert.Empty(model.Models);
    }

    [Fact]
    public async Task SaveCommand_SelectionMadeWhileFiltered_IsSaved()
    {
        var model = await OpenAsync();
        model.SearchText = "gamma";
        model.Models[0].IsSelected = true;
        model.SearchText = "";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new[] { Ref("vendor/beta"), Ref("other/gamma"), Ref(RemovedModel) }, _saved!.Settings.ImageModels);
    }

    [Fact]
    public async Task SaveCommand_SavesCheckedModelsInCatalogOrderAndKeepsUnknownOnes()
    {
        var model = await OpenAsync();
        model.Models[1].IsSelected = false;
        model.Models[2].IsSelected = true;
        model.Models[0].IsSelected = true;

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new[] { Ref("vendor/alpha"), Ref("other/gamma"), Ref(RemovedModel) }, _saved!.Settings.ImageModels);
        Assert.Equal(3, _saved.Settings.RunsPerModel);
        Assert.Equal(Ref(AssistantModel), _saved.Settings.AssistantModel);
        await _navigation.Received(1).GoBackAsync();
    }

    [Fact]
    public async Task SaveCommand_ChangedRuns_SavesNewRunsPerModel()
    {
        var model = await OpenAsync();
        model.RunsPerModel = 5;

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Equal(5, _saved!.Settings.RunsPerModel);
    }

    [Fact]
    public async Task RunsPerModel_ChangedByStepper_UpdatesText()
    {
        var model = await OpenAsync();

        model.RunsPerModel = 7;

        Assert.Equal("Runs per model: 7", model.RunsPerModelText);
    }

    [Fact]
    public void RunsPerModelLimits_MatchSessionSettings()
    {
        var model = CreateModel();

        Assert.Equal(SessionSettings.MinRunsPerModel, model.MinRunsPerModel);
        Assert.Equal(SessionSettings.MaxRunsPerModel, model.MaxRunsPerModel);
    }

    [Fact]
    public async Task RefreshCommand_ForcesCatalogReloadAndKeepsCurrentCheckboxes()
    {
        var model = await OpenAsync();
        model.Models[2].IsSelected = true;
        model.Models[0].IsSelected = false;

        await model.RefreshCommand.ExecuteAsync(null);

        await _provider.Received(1).ListImageModelsAsync(Arg.Any<CancellationToken>());
        await _modelRepository.Received(1).ReplaceAllAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ImageModel>>(), Arg.Any<CancellationToken>());
        Assert.Equal(new[] { "vendor/alpha", "vendor/beta", "other/gamma" }, Ids(model.Models));
        Assert.False(model.Models[0].IsSelected);
        Assert.True(model.Models[1].IsSelected);
        Assert.True(model.Models[2].IsSelected);
    }

    [Fact]
    public async Task RefreshCommand_KeepsSelectionWhileFiltered()
    {
        var model = await OpenAsync();
        model.SearchText = "beta";
        model.Models[0].IsSelected = false;
        model.SearchText = "";

        await model.RefreshCommand.ExecuteAsync(null);

        Assert.False(model.Models.Single(m => m.Id == "vendor/beta").IsSelected);
    }

    [Fact]
    public async Task OnAppearingAsync_UnknownSession_ShowsAlertAndNoModels()
    {
        _sessions.GetAsync(SessionId, Arg.Any<CancellationToken>()).Returns((Session?)null);
        var model = CreateModel();

        await model.OnAppearingAsync();

        Assert.Empty(model.Models);
        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, Arg.Any<string>());
        await _provider.DidNotReceive().ListImageModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_CatalogFails_ShowsAlert()
    {
        _modelRepository.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _provider.ListImageModelsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Network down"));
        var model = CreateModel();

        await model.OnAppearingAsync();

        Assert.Empty(model.Models);
        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, "Network down");
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_ServiceFails_ShowsAlertAndStaysOnPage()
    {
        var model = await OpenAsync();
        _sessions.UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Disk full"));

        await model.SaveCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, "Disk full");
        await _navigation.DidNotReceive().GoBackAsync();
    }

    [Fact]
    public async Task SaveAndRefresh_CannotRunWhileLoading()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<ImageModel>>();
        _modelRepository.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        _provider.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns(_ => gate.Task);
        var model = CreateModel();

        var loading = model.OnAppearingAsync();

        Assert.True(model.IsBusy);
        Assert.False(model.SaveCommand.CanExecute(null));
        Assert.False(model.RefreshCommand.CanExecute(null));

        gate.SetResult([Alpha]);
        await loading;

        Assert.False(model.IsBusy);
        Assert.True(model.SaveCommand.CanExecute(null));
        Assert.True(model.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyQueryAttributes_InvalidSessionId_Throws()
    {
        var model = new SessionModelsPageModel(_sessionService, _catalog, _navigation, _dialogs);

        Assert.Throws<ArgumentException>(() =>
            model.ApplyQueryAttributes(new Dictionary<string, object> { [Routes.SessionIdParameter] = "not-a-guid" }));
    }
}
