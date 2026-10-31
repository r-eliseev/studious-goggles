using ImageGen.Core.Domain.Chat;
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
    private readonly IConversationSummaryRepository _summaries = Substitute.For<IConversationSummaryRepository>();
    private readonly ISessionGenerationCoordinator _generationCoordinator = Substitute.For<ISessionGenerationCoordinator>();
    private readonly SessionChangeNotifier _changes = new(NullLogger<SessionChangeNotifier>.Instance);
    private readonly IAssistantChatClient _chat = Substitute.For<IAssistantChatClient>();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _service = new SessionService(_sessions, _messages, _runs, _files, Catalog(AssistantWithTools), time, _summaries, _changes, _generationCoordinator);
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
    public async Task CreateAsync_NotifiesSessionChanged()
    {
        var notified = new List<Guid>();
        var added = 0;
        _changes.SessionChanged += (_, sessionId) =>
        {
            added++;
            notified.Add(sessionId);
        };

        var session = await _service.CreateAsync(TestContext.Current.CancellationToken);

        // Exactly one notification, with the id of the created session, and the write has happened by then.
        Assert.Equal([session.Id], notified);
        Assert.Equal(1, added);
        await _sessions.Received(1).AddAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_AddFails_DoesNotNotify()
    {
        var notified = 0;
        _changes.SessionChanged += (_, _) => notified++;
        _sessions.AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("storage is down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0, notified);
    }

    [Fact]
    public async Task CreateAsync_ThrowingSubscriber_StillReturnsSessionAndCallsNextHandler()
    {
        var called = 0;
        _changes.SessionChanged += (_, _) => throw new InvalidOperationException("subscriber failed");
        _changes.SessionChanged += (_, _) => called++;

        var session = await _service.CreateAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(1, called);
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
            TimeProvider.System, Substitute.For<IConversationSummaryRepository>(), _changes, _generationCoordinator);
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
        var service = new SessionService(_sessions, _messages, _runs, _files, catalog, TimeProvider.System, Substitute.For<IConversationSummaryRepository>(), _changes, _generationCoordinator);
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
        var calls = new List<string>();
        _generationCoordinator.StopSessionAsync(id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls.Add("stop");
            return Task.CompletedTask;
        });
        _sessions.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls.Add("database");
            return Task.CompletedTask;
        });
        _files.DeleteSessionFilesAsync(id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            calls.Add("files");
            return Task.CompletedTask;
        });
        _changes.SessionChanged += (_, _) => calls.Add("notify");

        await _service.DeleteAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal(["stop", "database", "files", "notify"], calls);
        await _generationCoordinator.Received(1).StopSessionAsync(id, CancellationToken.None);
        await _sessions.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
        await _files.Received(1).DeleteSessionFilesAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_PreCancelledCaller_DoesNotStopOrDelete()
    {
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        var id = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.DeleteAsync(id, caller.Token));

        await _generationCoordinator.DidNotReceive().StopSessionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _sessions.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _files.DidNotReceive().DeleteSessionFilesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_LateCallerCancellation_DoesNotInterruptCleanup()
    {
        var id = Guid.NewGuid();
        using var caller = new CancellationTokenSource();
        var stopEntered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _generationCoordinator.StopSessionAsync(id, Arg.Any<CancellationToken>()).Returns(call =>
        {
            stopEntered.TrySetResult(call.Arg<CancellationToken>());
            return releaseStop.Task;
        });

        var deletion = _service.DeleteAsync(id, caller.Token);
        var cleanupToken = await stopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        caller.Cancel();
        releaseStop.SetResult();
        await deletion;

        Assert.Equal(CancellationToken.None, cleanupToken);
        await _sessions.Received(1).DeleteAsync(id, cleanupToken);
        await _files.Received(1).DeleteSessionFilesAsync(id, cleanupToken);
    }

    [Fact]
    public async Task DeleteAsync_StopFails_DoesNotDeleteOrNotify()
    {
        var id = Guid.NewGuid();
        var notified = false;
        _changes.SessionChanged += (_, _) => notified = true;
        _generationCoordinator.StopSessionAsync(id, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("stop failed"));

        await Assert.ThrowsAsync<IOException>(() => _service.DeleteAsync(id, TestContext.Current.CancellationToken));

        await _sessions.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _files.DidNotReceive().DeleteSessionFilesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Assert.False(notified);
    }

    [Fact]
    public async Task DeleteAsync_DatabaseDeleteFails_RetryDeletesFiles()
    {
        var id = Guid.NewGuid();
        var calls = 0;
        _sessions.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new IOException("database failed");
            }

            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<IOException>(() => _service.DeleteAsync(id, TestContext.Current.CancellationToken));
        await _files.DidNotReceive().DeleteSessionFilesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _service.DeleteAsync(id, TestContext.Current.CancellationToken);

        await _sessions.Received(2).DeleteAsync(id, CancellationToken.None);
        await _files.Received(1).DeleteSessionFilesAsync(id, CancellationToken.None);
    }

    [Fact]
    public async Task DeleteAsync_FileDeleteFails_RetryCompletesAndNotifiesOnlyAfterCleanup()
    {
        var id = Guid.NewGuid();
        var calls = 0;
        var notifications = 0;
        _files.DeleteSessionFilesAsync(id, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new IOException("file cleanup failed");
            }

            return Task.CompletedTask;
        });
        _changes.SessionChanged += (_, _) => notifications++;

        await Assert.ThrowsAsync<IOException>(() => _service.DeleteAsync(id, TestContext.Current.CancellationToken));
        Assert.Equal(0, notifications);
        await _service.DeleteAsync(id, TestContext.Current.CancellationToken);

        await _sessions.Received(2).DeleteAsync(id, CancellationToken.None);
        await _files.Received(2).DeleteSessionFilesAsync(id, CancellationToken.None);
        Assert.Equal(1, notifications);
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
    public async Task GetSummaryAsync_ReturnsSavedSummary()
    {
        var session = ExistingSession();
        var summary = new ConversationSummary
        {
            SessionId = session.Id,
            Text = "earlier decisions",
            CoversUpToMessageId = Guid.NewGuid(),
            Model = new ModelRef(ProviderIds.OpenRouter, "vendor/assistant"),
            CreatedAt = Now,
        };
        _summaries.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(summary);

        Assert.Same(summary, await _service.GetSummaryAsync(session.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSummaryAsync_NoSummary_ReturnsNull()
    {
        var session = ExistingSession();
        _summaries.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns((ConversationSummary?)null);

        Assert.Null(await _service.GetSummaryAsync(session.Id, TestContext.Current.CancellationToken));
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

    [Fact]
    public async Task RenameAsync_SetsTrimmedTitle()
    {
        var session = ExistingSession("Old");
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        await _service.RenameAsync(session.Id, "  Lighthouse  ", TestContext.Current.CancellationToken);

        Assert.Equal("Lighthouse", session.Title);
        await _sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_ThrowingSubscriber_StillWritesAndCallsNextHandler()
    {
        var session = ExistingSession("Old");
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        var called = 0;
        _changes.SessionChanged += (_, _) => throw new InvalidOperationException("подписчик упал");
        _changes.SessionChanged += (_, _) => called++;

        await _service.RenameAsync(session.Id, "Lighthouse", TestContext.Current.CancellationToken);

        // The write went through, the method finished, and the next subscriber was called.
        Assert.Equal("Lighthouse", session.Title);
        await _sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
        Assert.Equal(1, called);
    }

    [Fact]
    public async Task RenameAsync_Blank_Throws()
    {
        var session = ExistingSession("Old");
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RenameAsync(session.Id, "   ", TestContext.Current.CancellationToken));
        Assert.Equal("Old", session.Title);
    }

    [Fact]
    public async Task RenameAsync_Long_TruncatesToMaxLength()
    {
        var session = ExistingSession("Old");
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        await _service.RenameAsync(session.Id, new string('x', 60), TestContext.Current.CancellationToken);

        Assert.Equal(new string('x', Session.MaxTitleLength), session.Title);
    }

    [Fact]
    public async Task RenameAsync_KeepsUpdatedAt()
    {
        var session = ExistingSession("Old");
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        await _service.RenameAsync(session.Id, "New", TestContext.Current.CancellationToken);

        Assert.Equal(Now.AddDays(-1), session.UpdatedAt);
    }

    [Fact]
    public async Task RenameAsync_AfterSave_NotifiesSessionChanged()
    {
        var session = ExistingSession("Old");
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        Guid? notified = null;
        _changes.SessionChanged += (_, id) => notified = id;

        await _service.RenameAsync(session.Id, "New", TestContext.Current.CancellationToken);

        Assert.Equal(session.Id, notified);
        await _sessions.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetTitleFromFirstMessage_AfterRename_KeepsName()
    {
        var session = ExistingSession("Old");
        _sessions.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        await _service.RenameAsync(session.Id, "Mine", TestContext.Current.CancellationToken);

        await _service.SetTitleFromFirstMessageAsync(session.Id, "first message", TestContext.Current.CancellationToken);

        Assert.Equal("Mine", session.Title);
    }
}
