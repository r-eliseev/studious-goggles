using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public class SessionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IChatMessageRepository _messages = Substitute.For<IChatMessageRepository>();
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly IAssistantChatClient _chat = Substitute.For<IAssistantChatClient>();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _service = new SessionService(_sessions, _messages, _runs, _files, Catalog(AssistantWithTools), time);
    }

    private static readonly ChatModel AssistantWithTools = new(
        SessionSettings.DefaultAssistantModelId, "Assistant", SupportsImageInput: true, ContextLength: 1000, SupportsTools: true);

    private ChatModelCatalogService Catalog(params ChatModel[] models)
    {
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ChatModel>>(models));
        var providers = Substitute.For<IProviderRouter>();
        providers.GetChatClient(ProviderIds.OpenRouter).Returns(_chat);
        return new ChatModelCatalogService(providers, NullLogger<ChatModelCatalogService>.Instance);
    }

    private static Session ExistingSession(string title = Session.NewSessionTitle) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        CreatedAt = Now.AddDays(-1),
        UpdatedAt = Now.AddDays(-1),
        Settings = SessionSettings.CreateDefault(),
    };

    [Fact]
    public async Task CreateAsync_NewSession_HasDefaultTitleAndSettings()
    {
        var session = await _service.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Session.NewSessionTitle, session.Title);
        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal([new ModelRef(ProviderIds.OpenRouter, SessionSettings.DefaultImageModelId)], session.Settings.ImageModels);
        await _sessions.Received(1).AddAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_NewSession_UsesClaudeHaikuAsAssistant()
    {
        var session = await _service.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new ModelRef(ProviderIds.OpenRouter, "anthropic/claude-haiku-5.5"), session.Settings.AssistantModel);
    }

    [Fact]
    public async Task UpdateSettingsAsync_AssistantWithoutTools_ThrowsAndKeepsSettings()
    {
        var session = ExistingSession();
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var service = new SessionService(
            _sessions, _messages, _runs, _files,
            Catalog(new ChatModel("vendor/plain", "Plain", SupportsImageInput: false, ContextLength: 1000)),
            TimeProvider.System);
        var settings = new SessionSettings
        {
            ImageModels = session.Settings.ImageModels,
            RunsPerModel = 1,
            AssistantModel = new ModelRef(ProviderIds.OpenRouter, "vendor/plain"),
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateSettingsAsync(session.Id, settings, TestContext.Current.CancellationToken));

        await _sessions.DidNotReceive().UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettingsAsync_AssistantNotInCatalog_Throws()
    {
        var session = ExistingSession();
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var settings = new SessionSettings
        {
            ImageModels = session.Settings.ImageModels,
            RunsPerModel = 1,
            AssistantModel = new ModelRef(ProviderIds.OpenRouter, "vendor/unknown"),
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateSettingsAsync(session.Id, settings, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateSettingsAsync_CatalogUnavailable_SavesWithoutCheck()
    {
        var session = ExistingSession();
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var catalog = Catalog();
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<ChatModel>>>(_ => throw new ProviderException(ErrorKind.Network, "down"));
        var service = new SessionService(_sessions, _messages, _runs, _files, catalog, TimeProvider.System);
        var settings = new SessionSettings
        {
            ImageModels = session.Settings.ImageModels,
            RunsPerModel = 1,
            AssistantModel = new ModelRef(ProviderIds.OpenRouter, "vendor/offline"),
        };

        var updated = await service.UpdateSettingsAsync(session.Id, settings, TestContext.Current.CancellationToken);

        Assert.Equal("vendor/offline", updated.Settings.AssistantModel.ModelId);
    }

    [Fact]
    public async Task DeleteAsync_DeletesRecordsAndFiles()
    {
        var id = Guid.NewGuid();

        await _service.DeleteAsync(id, TestContext.Current.CancellationToken);

        await _sessions.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
        await _files.Received(1).DeleteSessionFilesAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettingsAsync_DuplicateModels_AreRemovedKeepingOrder()
    {
        var session = ExistingSession();
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var settings = new SessionSettings
        {
            ImageModels = [ModelRef.Parse("a"), ModelRef.Parse("b"), ModelRef.Parse("a")],
            RunsPerModel = 3,
        };

        var updated = await _service.UpdateSettingsAsync(session.Id, settings, TestContext.Current.CancellationToken);

        Assert.Equal([ModelRef.Parse("a"), ModelRef.Parse("b")], updated.Settings.ImageModels);
        Assert.Equal(3, updated.Settings.RunsPerModel);
        Assert.Equal(Now, updated.UpdatedAt);
        await _sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task UpdateSettingsAsync_RunsPerModelOutOfRange_Throws(int runs)
    {
        var settings = new SessionSettings { ImageModels = [ModelRef.Parse("a")], RunsPerModel = runs };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.UpdateSettingsAsync(Guid.NewGuid(), settings, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetTitleFromFirstMessageAsync_DefaultTitle_UsesFirst40Characters()
    {
        var session = ExistingSession();
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var text = new string('x', 50);

        await _service.SetTitleFromFirstMessageAsync(session.Id, text, TestContext.Current.CancellationToken);

        Assert.Equal(new string('x', 40), session.Title);
        Assert.Equal(Now, session.UpdatedAt);
    }

    [Fact]
    public async Task SetTitleFromFirstMessageAsync_CustomTitle_KeepsTitle()
    {
        var session = ExistingSession("My fox");
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        await _service.SetTitleFromFirstMessageAsync(session.Id, "something else", TestContext.Current.CancellationToken);

        Assert.Equal("My fox", session.Title);
    }

    [Fact]
    public async Task UpdateGenerationParametersAsync_ValidAspectRatio_SavesParametersKeepsModelsAndRuns()
    {
        var session = ExistingSession();
        var models = session.Settings.ImageModels.ToList();
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var saved = await _service.UpdateGenerationParametersAsync(
            session.Id, new GenerationParameters { AspectRatio = "3:2" }, TestContext.Current.CancellationToken);

        Assert.Equal("3:2", saved.Settings.Parameters.AspectRatio);
        Assert.Equal(models, saved.Settings.ImageModels);
        Assert.Equal(SessionSettings.MinRunsPerModel, saved.Settings.RunsPerModel);
        await _sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("gif", null)]
    [InlineData(null, "0x")]
    public async Task UpdateGenerationParametersAsync_InvalidValue_ThrowsAndDoesNotSave(string? outputFormat, string? aspectRatio)
    {
        var session = ExistingSession();
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var parameters = new GenerationParameters { OutputFormat = outputFormat, AspectRatio = aspectRatio };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateGenerationParametersAsync(session.Id, parameters, TestContext.Current.CancellationToken));

        await _sessions.DidNotReceive().UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettingsAsync_KeepsSavedParameters()
    {
        var session = ExistingSession();
        session.Settings.Parameters = new GenerationParameters { AspectRatio = "3:2" };
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var newSettings = new SessionSettings
        {
            ImageModels = [new ModelRef(ProviderIds.OpenRouter, "vendor/other")],
            RunsPerModel = 2,
            AssistantModel = session.Settings.AssistantModel,
        };

        var updated = await _service.UpdateSettingsAsync(session.Id, newSettings, TestContext.Current.CancellationToken);

        Assert.Equal("3:2", updated.Settings.Parameters.AspectRatio);
        Assert.Equal("vendor/other", updated.Settings.ImageModels.Single().ModelId);
    }

    [Fact]
    public async Task GetTimelineAsync_ReturnsAllParts()
    {
        var session = ExistingSession();
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _messages.ListBySessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns([]);
        _runs.ListRunsBySessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns([]);
        _runs.ListGenerationsBySessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns([]);

        var timeline = await _service.GetTimelineAsync(session.Id, TestContext.Current.CancellationToken);

        Assert.Same(session, timeline.Session);
        Assert.Empty(timeline.Messages);
    }
}
