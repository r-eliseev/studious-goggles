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
    private readonly IVideoModelRepository _videoModelRepository = Substitute.For<IVideoModelRepository>();
    private readonly IVideoModelProvider _videoProvider = Substitute.For<IVideoModelProvider>();
    private readonly IAudioModelRepository _audioModelRepository = Substitute.For<IAudioModelRepository>();
    private readonly VideoModelCatalogService _videoCatalog;
    private readonly AudioModelCatalogService _audioCatalog;
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly Session _session;
    private readonly SessionParametersPageModel _model;
    private Session? _saved;

    public SessionParametersPageModelTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var router = ProviderDoubles.Router();
        router.GetVideoModelProvider(ProviderIds.OpenRouter).Returns(_videoProvider);
        var audioProvider = Substitute.For<IAudioModelProvider>();
        router.GetAudioModelProvider(ProviderIds.OpenRouter).Returns(audioProvider);
        _videoCatalog = new VideoModelCatalogService(router, _videoModelRepository, NullLogger<VideoModelCatalogService>.Instance);
        _audioCatalog = new AudioModelCatalogService(router, _audioModelRepository, NullLogger<AudioModelCatalogService>.Instance);
        var sessionService = new SessionService(
            _sessions,
            Substitute.For<IChatMessageRepository>(),
            Substitute.For<IRunRepository>(),
            Substitute.For<IImageFileStore>(),
            new ChatModelCatalogService(router, NullLogger<ChatModelCatalogService>.Instance),
            time, Substitute.For<IConversationSummaryRepository>(), new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance), Substitute.For<ISessionGenerationCoordinator>(), DefaultAssistantModels.Create(),
            _videoCatalog, _audioCatalog);
        var catalog = new ModelCatalogService(
            ProviderDoubles.Router(), _modelRepository, NullLogger<ModelCatalogService>.Instance);
        _model = new SessionParametersPageModel(sessionService, catalog, _navigation, ErrorAlerts.Create(_dialogs), _videoCatalog, _audioCatalog);
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
        _videoModelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
        [
            Video("vendor/alpha-video", [4, 6, 8], ["720p", "1080p"], ["16:9", "1:1"], generatesAudio: true),
            Video("vendor/beta-video", [5, 6, 8, 10], ["1080p", "4k"], ["16:9"], generatesAudio: false),
        ]);
        _audioModelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
        [
            new AudioModel
            {
                ProviderId = ProviderIds.OpenRouter,
                Id = "speech/model",
                Name = "Speech",
                Output = AudioOutput.Speech,
                FetchedAt = Now,
            },
        ]);
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
    public async Task Cancel_GoesBackWithoutSaving()
    {
        await _model.OnAppearingAsync();
        _model.SelectedAspectRatio = _model.AspectRatioOptions.Single(o => o.Value == "3:2");

        await _model.CancelCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoBackAsync();
        Assert.Null(_saved);
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

    [Fact]
    public async Task OnAppearingAsync_VideoParameters_UsesCommonValuesAndAudioVisibility()
    {
        _session.Settings.VideoModels = [
            new ModelRef(ProviderIds.OpenRouter, "vendor/alpha-video"),
            new ModelRef(ProviderIds.OpenRouter, "vendor/beta-video"),
        ];
        _session.Settings.VideoParameters = new VideoParameters { DurationSeconds = 8, Resolution = "1080p", AspectRatio = "16:9", GenerateAudio = false };

        await _model.OnAppearingAsync();

        Assert.Equal(["6", "8"], _model.VideoDurationOptions.Select(option => option.Value));
        Assert.Equal([null, "1080p"], _model.VideoResolutionOptions.Select(option => option.Value));
        Assert.Equal([null, "16:9"], _model.VideoAspectRatioOptions.Select(option => option.Value));
        Assert.True(_model.IsVideoAudioOptionVisible);
        Assert.False(_model.GenerateVideoAudio);
    }

    [Fact]
    public async Task OnAppearingAsync_NoSelectedModelSupportsAudio_HidesAudioSwitch()
    {
        _session.Settings.VideoModels = [new ModelRef(ProviderIds.OpenRouter, "vendor/beta-video")];

        await _model.OnAppearingAsync();

        Assert.False(_model.IsVideoAudioOptionVisible);
    }

    [Fact]
    public async Task OnAppearingAsync_SelectedSpeechModel_ShowsSavedVoice()
    {
        _session.Settings.AudioModels = [new ModelRef(ProviderIds.OpenRouter, "speech/model")];
        _session.Settings.AudioParameters = new AudioParameters { Voice = "alloy" };

        await _model.OnAppearingAsync();

        Assert.True(_model.IsAudioVoiceVisible);
        Assert.Equal("alloy", _model.AudioVoice);
    }

    [Fact]
    public async Task SaveCommand_AudioVoice_SavesWithSelectedAudioModels()
    {
        _session.Settings.AudioModels = [new ModelRef(ProviderIds.OpenRouter, "speech/model")];
        await _model.OnAppearingAsync();
        _model.AudioVoice = "alloy";

        await _model.SaveCommand.ExecuteAsync(null);

        Assert.Equal("alloy", _saved!.Settings.AudioParameters.Voice);
    }

    private static ImageModel WithParameters(string id, string name, string parameterName, params string[] values)
    {
        var model = TestModels.Model(id, name);
        model.Parameters = [new ModelParameter(parameterName, ParameterKind.Enum, values)];
        return model;
    }

    private static VideoModel Video(string id, IReadOnlyList<int> durations, IReadOnlyList<string> resolutions,
        IReadOnlyList<string> aspectRatios, bool generatesAudio) => new()
        {
            ProviderId = ProviderIds.OpenRouter,
            Id = id,
            Name = id,
            Durations = durations,
            Resolutions = resolutions,
            AspectRatios = aspectRatios,
            FrameImages = [],
            GeneratesAudio = generatesAudio,
            PriceSkus = new Dictionary<string, decimal>(),
            FetchedAt = Now,
        };
}
