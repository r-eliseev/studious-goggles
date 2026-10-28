using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;

namespace ImageGen.Core.Tests.Services;

public class SessionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IChatMessageRepository _messages = Substitute.For<IChatMessageRepository>();
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        _service = new SessionService(_sessions, _messages, _runs, _files, time);
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
