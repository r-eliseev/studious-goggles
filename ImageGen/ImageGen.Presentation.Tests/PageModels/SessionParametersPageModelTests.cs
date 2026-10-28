using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Tests.PageModels;

public class SessionParametersPageModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId = Guid.Parse("7c1e0a52-4d6b-4f3a-8e2d-9a1b2c3d4e5f");

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IImageModelRepository _modelRepository = Substitute.For<IImageModelRepository>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly Session _session;
    private readonly SessionParametersPageModel _model;
    private Session? _saved;

    public SessionParametersPageModelTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var sessionService = new SessionService(
            _sessions,
            Substitute.For<IChatMessageRepository>(),
            Substitute.For<IRunRepository>(),
            Substitute.For<IImageFileStore>(),
            new ChatModelCatalogService(ProviderDoubles.Router(), NullLogger<ChatModelCatalogService>.Instance),
            time);
        var catalog = new ModelCatalogService(
            ProviderDoubles.Router(), _modelRepository, NullLogger<ModelCatalogService>.Instance);
        _model = new SessionParametersPageModel(sessionService, catalog, _navigation, _dialogs);
        _model.ApplyQueryAttributes(new Dictionary<string, object> { [Routes.SessionIdParameter] = SessionId.ToString("D") });

        _session = new Session
        {
            Id = SessionId,
            Title = "Fox",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = new SessionSettings
            {
                ImageModels = [new ModelRef(ProviderIds.OpenRouter, "vendor/alpha")],
            },
        };
        _sessions.GetAsync(SessionId, Arg.Any<CancellationToken>()).Returns(_session);
        _sessions.UpdateAsync(Arg.Do<Session>(s => _saved = s), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<IReadOnlyList<ImageModel>>(
            [
                WithParameters("vendor/alpha", "Alpha", "aspect_ratio", "1:1", "3:2", "auto"),
                WithParameters("vendor/beta", "Beta", "aspect_ratio", "4:3"),
            ]));
    }

    [Fact]
    public async Task OnAppearingAsync_ValuesOfSelectedModelsOnly()
    {
        await _model.OnAppearingAsync();

        var values = _model.AspectRatioOptions.Select(o => o.Value).ToList();
        Assert.Equal([null, "1:1", "3:2", "auto"], values);
    }

    [Fact]
    public async Task OnAppearingAsync_SavedValueNotInCatalog_StaysSelectable()
    {
        _session.Settings.Parameters = new GenerationParameters { AspectRatio = "5:4" };

        await _model.OnAppearingAsync();

        Assert.Contains(_model.AspectRatioOptions, o => o.Value == "5:4");
        Assert.Equal("5:4", _model.SelectedAspectRatio!.Value);
    }

    [Fact]
    public async Task OnAppearingAsync_OutputFormatOffersPngJpegWebp()
    {
        await _model.OnAppearingAsync();

        Assert.Equal([null, "png", "jpeg", "webp"], _model.OutputFormatOptions.Select(o => o.Value));
    }

    [Fact]
    public async Task SaveCommand_SelectedValues_SavesParametersAndGoesBack()
    {
        await _model.OnAppearingAsync();
        _model.SelectedAspectRatio = _model.AspectRatioOptions.Single(o => o.Value == "3:2");
        _model.SelectedOutputFormat = _model.OutputFormatOptions.Single(o => o.Value == "webp");

        await _model.SaveCommand.ExecuteAsync(null);

        Assert.Equal("3:2", _saved!.Settings.Parameters.AspectRatio);
        Assert.Equal("webp", _saved.Settings.Parameters.OutputFormat);
        await _navigation.Received(1).GoBackAsync();
    }

    [Fact]
    public async Task SaveCommand_ModelDefault_SavesNull()
    {
        _session.Settings.Parameters = new GenerationParameters { AspectRatio = "1:1" };
        await _model.OnAppearingAsync();
        _model.SelectedAspectRatio = _model.AspectRatioOptions.Single(o => o.Value is null);

        await _model.SaveCommand.ExecuteAsync(null);

        Assert.Null(_saved!.Settings.Parameters.AspectRatio);
    }

    private static ImageModel WithParameters(string id, string name, string parameterName, params string[] values)
    {
        var model = TestModels.Model(id, name);
        model.Parameters = [new ModelParameter(parameterName, ParameterKind.Enum, values)];
        return model;
    }
}
