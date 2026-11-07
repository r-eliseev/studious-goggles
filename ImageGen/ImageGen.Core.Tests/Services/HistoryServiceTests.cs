using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public sealed class HistoryServiceTests
{
    [Fact]
    public async Task ClearAllAsync_StopsEverySessionBeforeDeletingAndNotifiesAll()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = new Session { Id = Guid.NewGuid(), Title = "First", CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch, Settings = SessionSettings.CreateDefault() };
        var second = new Session { Id = Guid.NewGuid(), Title = "Second", CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch, Settings = SessionSettings.CreateDefault() };
        var sessions = Substitute.For<ISessionRepository>();
        var files = Substitute.For<IImageFileStore>();
        var coordinator = Substitute.For<ISessionGenerationCoordinator>();
        var index = Substitute.For<ISemanticIndexController>();
        index.GetStatusSnapshot().Returns(new SemanticIndexStatus(false, 0, 0, SemanticIndexState.Off));
        var changes = new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance);
        var calls = new List<string>();
        sessions.ListAsync(ct).Returns([first, second]);
        coordinator.StopSessionAsync(first.Id, Arg.Any<CancellationToken>()).Returns(_ => { calls.Add("stop-first"); return Task.CompletedTask; });
        coordinator.StopSessionAsync(second.Id, Arg.Any<CancellationToken>()).Returns(_ => { calls.Add("stop-second"); return Task.CompletedTask; });
        sessions.DeleteAllAsync(Arg.Any<CancellationToken>()).Returns(_ => { calls.Add("delete-db"); return Task.FromResult(2); });
        files.DeleteAllMediaAsync(Arg.Any<CancellationToken>()).Returns(_ => { calls.Add("delete-files"); return Task.FromResult(3); });
        changes.AllSessionsChanged += (_, _) => calls.Add("notify-all");
        var service = new HistoryService(sessions, files, coordinator, changes, NullLogger<HistoryService>.Instance, index);

        var count = await service.ClearAllAsync(ct);

        Assert.Equal(2, count);
        Assert.Equal(["stop-first", "stop-second", "delete-db", "delete-files", "notify-all"], calls);
    }

    [Fact]
    public async Task ClearAllAsync_EnabledSearchRefreshesStatusWithoutDisablingIt()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        sessions.DeleteAllAsync(Arg.Any<CancellationToken>()).Returns(0);
        var files = Substitute.For<IImageFileStore>();
        files.DeleteAllMediaAsync(Arg.Any<CancellationToken>()).Returns(0);
        var index = Substitute.For<ISemanticIndexController>();
        index.GetStatusSnapshot().Returns(new SemanticIndexStatus(true, 1, 0, SemanticIndexState.Ready));
        var service = new HistoryService(
            sessions, files, Substitute.For<ISessionGenerationCoordinator>(),
            new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance),
            NullLogger<HistoryService>.Instance, index);

        await service.ClearAllAsync(TestContext.Current.CancellationToken);

        await index.Received(1).GetStatusAsync(CancellationToken.None);
        Assert.True(index.GetStatusSnapshot().Enabled);
        await index.DidNotReceive().DisableAsync(Arg.Any<CancellationToken>());
    }
}
