using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Search;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Data;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace ImageGen.Presentation.Tests.PageModels;

public class SessionsPageModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
    private readonly SqliteDatabase _database;
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
    private readonly IModelProvider _provider = Substitute.For<IModelProvider>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ISearchTextSource _searchTexts = Substitute.For<ISearchTextSource>();
    private readonly IMainThread _mainThread = Substitute.For<IMainThread>();
    private readonly SessionChangeNotifier _changes = new(NullLogger<SessionChangeNotifier>.Instance);

    public SessionsPageModelTests()
    {
        _database = new SqliteDatabase(Path.Combine(_directory, SqliteDatabase.FileName));
        _runs.MarkUnfinishedAsInterruptedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns("sk-or-saved");
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        _mainThread.When(thread => thread.BeginInvokeOnMainThread(Arg.Any<Action>()))
            .Do(call => call.Arg<Action>().Invoke());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task OnAppearingAsync_NoKey_NavigatesToKeyPageAndDoesNotListSessions()
    {
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns((string?)null);
        var model = CreateModel();

        await model.OnAppearingAsync();

        await _navigation.Received(1).GoToAsync(Routes.ApiKeyFor(ProviderIds.OpenRouter));
        await _sessionRepository.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
        Assert.Empty(model.Sessions);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task OnAppearingAsync_KeySaved_ListsSessionsNewestFirst()
    {
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            NewSession("Newest", new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)),
            NewSession("Older", new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero)),
        ]);
        var model = CreateModel();

        await model.OnAppearingAsync();

        Assert.Equal(["Newest", "Older"], model.Sessions.Select(item => item.Title));
        await _navigation.DidNotReceive().GoToAsync(Arg.Any<string>());
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task OnAppearingAsync_SplitsTodayAndEarlier()
    {
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(now);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            NewSession("Fresh", now.AddMinutes(-5)),
            NewSession("Old", now.AddDays(-3)),
        ]);
        var model = CreateModel(clock);
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns("sk-or-saved");

        await model.OnAppearingAsync();

        Assert.Equal(["Fresh"], model.Today.Select(item => item.Title));
        Assert.Equal(["Old"], model.Earlier.Select(item => item.Title));
        Assert.Equal(2, model.Sessions.Count);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task OnAppearingAsync_NoSessions_LeavesListEmpty()
    {
        var model = CreateModel();

        await model.OnAppearingAsync();

        Assert.Empty(model.Sessions);
    }

    [Fact]
    public async Task OnAppearingAsync_CalledAgain_ReplacesRowsWithoutDuplicates()
    {
        var session = NewSession("Only", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        var model = CreateModel();

        await model.OnAppearingAsync();
        await model.OnAppearingAsync();

        Assert.Single(model.Sessions);
    }

    [Fact]
    public async Task SessionChanged_NewTitle_UpdatesExistingRow()
    {
        var session = NewSession("New session", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _sessionRepository.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _runs.CountRunsBySessionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int>());
        var model = CreateModel();
        await model.OnAppearingAsync();
        var row = Assert.Single(model.Sessions);
        session.Title = "blue moon";

        _changes.Notify(session.Id);
        await WaitUntilAsync(() => row.Title == "blue moon");

        Assert.Same(row, Assert.Single(model.Sessions));
    }

    [Fact]
    public async Task SessionChanged_NewRun_UpdatesRunCount()
    {
        var session = NewSession("New session", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _sessionRepository.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _runs.CountRunsBySessionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int>());
        var model = CreateModel();
        await model.OnAppearingAsync();
        var row = Assert.Single(model.Sessions);
        Assert.Equal(0, row.RunCount);

        _runs.CountRunsBySessionAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [session.Id] = 1 });
        _changes.Notify(session.Id);
        await WaitUntilAsync(() => row.RunCount == 1);

        Assert.Same(row, Assert.Single(model.Sessions));
        Assert.EndsWith("1 run", row.DetailsText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionChanged_UnknownSession_InsertsInTimestampOrder()
    {
        var first = NewSession("First", DateTimeOffset.UtcNow);
        var added = NewSession("Added", first.UpdatedAt.AddMinutes(-1));
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([first]);
        _sessionRepository.GetAsync(added.Id, Arg.Any<CancellationToken>()).Returns(added);
        _runs.CountRunsBySessionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int>());
        var model = CreateModel();
        await model.OnAppearingAsync();

        _changes.Notify(added.Id);
        await WaitUntilAsync(() => model.Sessions.Count == 2);

        Assert.Equal([first.Id, added.Id], model.Sessions.Select(item => item.Id));
    }

    [Fact]
    public async Task SessionChanged_DeletedSession_RemovesRowFromEveryList()
    {
        var session = NewSession("Deleted", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _sessionRepository.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns((Session?)null);
        var model = CreateModel();
        await model.OnAppearingAsync();

        _changes.Notify(session.Id);
        await WaitUntilAsync(() => model.Sessions.Count == 0);

        Assert.Empty(model.Today);
        Assert.Empty(model.Earlier);
        Assert.Empty(model.SearchResults);
    }

    [Fact]
    public async Task SessionChanged_WhileSearching_KeepsQueryAndResultsAndUpdatesTitle()
    {
        var session = NewSession("Animals", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _sessionRepository.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _runs.CountRunsBySessionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int>());
        _searchTexts.ListAllAsync(Arg.Any<CancellationToken>()).Returns([new SearchText(session.Id, SearchTextKind.Prompt, "blue moon")]);
        var model = CreateModel();
        await model.OnAppearingAsync();
        model.SearchText = "moon";
        await WaitUntilAsync(() => model.SearchResults.Count == 1);
        session.Title = "blue moon";

        _changes.Notify(session.Id);
        await WaitUntilAsync(() => model.Sessions.Single().Title == "blue moon");

        Assert.Equal("moon", model.SearchText);
        Assert.Same(model.Sessions.Single(), Assert.Single(model.SearchResults));
    }

    [Fact]
    public async Task SessionChanged_EarlierUpdatedToday_MovesToTopOfToday()
    {
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(now);
        var today = NewSession("Today", now.AddMinutes(-5));
        var earlier = NewSession("Earlier", now.AddDays(-1));
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([today, earlier]);
        _sessionRepository.GetAsync(earlier.Id, Arg.Any<CancellationToken>()).Returns(earlier);
        _runs.CountRunsBySessionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int>());
        var model = CreateModel(clock);
        await model.OnAppearingAsync();
        earlier.UpdatedAt = now;

        _changes.Notify(earlier.Id);
        await WaitUntilAsync(() => model.Today.FirstOrDefault()?.Id == earlier.Id);

        Assert.DoesNotContain(model.Earlier, item => item.Id == earlier.Id);
        Assert.Equal(earlier.Id, model.Sessions[0].Id);
    }

    [Fact]
    public async Task OnAppearingAsync_Twice_OnlySubscribesOnceToSessionChanges()
    {
        var session = NewSession("One", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _sessionRepository.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _runs.CountRunsBySessionAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int>());
        var model = CreateModel();
        await model.OnAppearingAsync();
        await model.OnAppearingAsync();

        _changes.Notify(session.Id);
        await WaitUntilAsync(() => rowGetCalls(session.Id) == 1);

        int rowGetCalls(Guid id) => _sessionRepository.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(ISessionRepository.GetAsync)
                && (Guid)call.GetArguments()[0]! == id);
    }

    [Fact]
    public async Task OnAppearingAsync_ListingFails_ShowsErrorAlert()
    {
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database is locked"));
        var model = CreateModel();

        await model.OnAppearingAsync();

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, "database is locked");
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task NewSessionCommand_CreatesSessionAndOpensChat()
    {
        Session? created = null;
        _sessionRepository.When(repository => repository.AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()))
            .Do(call => created = call.Arg<Session>());
        var model = CreateModel();

        await model.NewSessionCommand.ExecuteAsync(null);

        Assert.NotNull(created);
        await _navigation.Received(1).GoToAsync(Routes.ChatFor(created!.Id));
    }

    [Fact]
    public async Task OpenSession_NavigatesToChatOfThatSession()
    {
        var session = NewSession("Open me", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        var model = CreateModel();
        await model.OnAppearingAsync();

        await model.Sessions[0].OpenCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.ChatFor(session.Id));
    }

    [Fact]
    public async Task DeleteSession_Confirmed_DeletesSessionFilesAndRow()
    {
        var session = NewSession("Delete me", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _dialogs.ConfirmAsync(AppResources.DeleteSessionTitle, AppResources.DeleteSessionMessage, AppResources.DeleteSession, AppResources.Cancel, true)
            .Returns(true);
        var model = CreateModel();
        await model.OnAppearingAsync();

        await model.Sessions[0].DeleteCommand.ExecuteAsync(null);

        await _sessionRepository.Received(1).DeleteAsync(session.Id, Arg.Any<CancellationToken>());
        await _files.Received(1).DeleteSessionFilesAsync(session.Id, Arg.Any<CancellationToken>());
        Assert.Empty(model.Sessions);
    }

    [Fact]
    public async Task OpenSettings_NavigatesToSettings()
    {
        var model = CreateModel();

        await model.OpenSettingsCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync(Routes.Settings);
    }

    [Fact]
    public async Task Delete_AsksDestructiveConfirm()
    {
        var session = NewSession("Delete me", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(false);
        var model = CreateModel();
        await model.OnAppearingAsync();

        await model.Sessions[0].DeleteCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync(
            AppResources.DeleteSessionTitle, AppResources.DeleteSessionMessage, AppResources.DeleteSession, AppResources.Cancel, true);
    }

    [Fact]
    public async Task OpenChat_MarksSessionCurrentAndAnotherChatMovesMark()
    {
        var first = NewSession("First", DateTimeOffset.UtcNow);
        var second = NewSession("Second", DateTimeOffset.UtcNow.AddMinutes(-1));
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([first, second]);
        var model = CreateModel();
        await model.OnAppearingAsync();

        _tracker.CurrentId = first.Id;
        Assert.True(model.Sessions[0].IsCurrent);
        Assert.False(model.Sessions[1].IsCurrent);

        _tracker.CurrentId = second.Id;
        Assert.False(model.Sessions[0].IsCurrent);
        Assert.True(model.Sessions[1].IsCurrent);
    }

    [Fact]
    public async Task OpenSession_ReplaceOnOpen_ReplacesInsteadOfPushing()
    {
        var session = NewSession("Replace me", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        var model = CreateModel();
        await model.OnAppearingAsync();
        model.ReplaceOnOpen = true;

        await model.Sessions[0].OpenCommand.ExecuteAsync(null);

        await _navigation.Received(1).ReplaceAsync(Routes.ChatFor(session.Id));
        await _navigation.DidNotReceive().GoToAsync(Routes.ChatFor(session.Id));
    }

    [Fact]
    public async Task DeleteSession_Cancelled_KeepsSessionAndRow()
    {
        var session = NewSession("Keep me", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()).Returns(false);
        var model = CreateModel();
        await model.OnAppearingAsync();

        await model.Sessions[0].DeleteCommand.ExecuteAsync(null);

        await _sessionRepository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Assert.Single(model.Sessions);
    }

    [Fact]
    public async Task OnAppearingAsync_ShowsRunCountPerSession()
    {
        var busy = NewSession("Busy", DateTimeOffset.UtcNow);
        var idle = NewSession("Idle", DateTimeOffset.UtcNow.AddMinutes(-1));
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([busy, idle]);
        _runs.CountRunsBySessionAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [busy.Id] = 3 });
        var model = CreateModel();

        await model.OnAppearingAsync();

        Assert.Equal(3, model.Sessions[0].RunCount);
        Assert.Equal(0, model.Sessions[1].RunCount);
    }

    [Fact]
    public async Task Rename_SavesNewTitleAndShowsIt()
    {
        var session = NewSession("Old name", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _sessionRepository.GetAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        _dialogs.PromptAsync(AppResources.RenameSessionTitle, "", AppResources.Rename, AppResources.Cancel, "Old name")
            .Returns("  Lighthouse  ");
        var model = CreateModel();
        await model.OnAppearingAsync();

        await model.Sessions[0].RenameCommand.ExecuteAsync(null);

        Assert.Equal("Lighthouse", model.Sessions[0].Title);
        await _sessionRepository.Received(1).UpdateAsync(
            Arg.Is<Session>(saved => saved.Title == "Lighthouse"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rename_Cancelled_KeepsTitle()
    {
        var session = NewSession("Keep name", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((string?)null);
        var model = CreateModel();
        await model.OnAppearingAsync();

        await model.Sessions[0].RenameCommand.ExecuteAsync(null);

        Assert.Equal("Keep name", model.Sessions[0].Title);
        await _sessionRepository.DidNotReceive().UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rename_Blank_ShowsErrorAndKeepsTitle()
    {
        var session = NewSession("Keep name", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("   ");
        var model = CreateModel();
        await model.OnAppearingAsync();

        await model.Sessions[0].RenameCommand.ExecuteAsync(null);

        Assert.Equal("Keep name", model.Sessions[0].Title);
        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.RenameEmpty);
        await _sessionRepository.DidNotReceive().UpdateAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_ShowsSessionWithSnippetOfMatchingText()
    {
        var fox = NewSession("Animals", DateTimeOffset.UtcNow);
        var boat = NewSession("Boats", DateTimeOffset.UtcNow.AddMinutes(-1));
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([fox, boat]);
        _searchTexts.ListAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new SearchText(fox.Id, SearchTextKind.Prompt, "red fox in snow"),
            new SearchText(boat.Id, SearchTextKind.Title, "Boats"),
        ]);
        var model = CreateModel();
        await model.OnAppearingAsync();

        model.SearchText = "fox";
        await WaitUntilAsync(() => model.SearchResults.Count > 0);

        var hit = Assert.Single(model.SearchResults);
        Assert.Same(model.Sessions[0], hit);
        Assert.Equal("prompt: red fox in snow", hit.SearchLine);
        Assert.False(model.HasNoResults);
    }

    [Fact]
    public async Task Search_Debounced_OnlyLastQueryRuns()
    {
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([NewSession("Fox", DateTimeOffset.UtcNow)]);
        var model = CreateModel();
        await model.OnAppearingAsync();

        model.SearchText = "f";
        model.SearchText = "fo";
        model.SearchText = "fox";
        await Task.Delay(SessionsPageModel.SearchDebounce + TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        await _searchTexts.Received(1).ListAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_NoHits_ShowsNothingFound()
    {
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([NewSession("Fox", DateTimeOffset.UtcNow)]);
        _searchTexts.ListAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        var model = CreateModel();
        await model.OnAppearingAsync();

        model.SearchText = "zebra";
        await WaitUntilAsync(() => model.HasNoResults);

        Assert.Empty(model.SearchResults);
        Assert.False(model.IsSearching);
    }

    [Fact]
    public async Task ClearSearch_EmptiesResultsAndSnippets()
    {
        var fox = NewSession("Fox", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([fox]);
        _searchTexts.ListAllAsync(Arg.Any<CancellationToken>()).Returns([new SearchText(fox.Id, SearchTextKind.Title, "Fox")]);
        var model = CreateModel();
        await model.OnAppearingAsync();
        model.SearchText = "fox";
        await WaitUntilAsync(() => model.SearchResults.Count > 0);

        model.ClearSearchCommand.Execute(null);

        Assert.Empty(model.SearchResults);
        Assert.False(model.HasNoResults);
        Assert.Equal("", model.Sessions[0].SearchLine);
        Assert.Single(model.Sessions);
    }

    [Fact]
    public async Task StartExample_CreatesSessionAndOpensChatWithMessage()
    {
        Session? created = null;
        _sessionRepository.When(repository => repository.AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>()))
            .Do(call => created = call.Arg<Session>());
        var model = CreateModel();

        await model.StartExampleCommand.ExecuteAsync(AppResources.EmptyChatExample1);

        Assert.NotNull(created);
        await _navigation.Received(1).GoToAsync(Routes.ChatWithMessage(created!.Id, AppResources.EmptyChatExample1));
    }

    [Fact]
    public async Task ShowsListExamples_OnlyWhenExamplesAreEnabledAndNoSessions()
    {
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        var model = CreateModel();
        await model.OnAppearingAsync();

        Assert.False(model.ShowsListExamples);
        model.ExamplesInList = true;
        Assert.True(model.ShowsListExamples);
    }

    [Fact]
    public void Examples_AreTheThreeStarterPrompts()
    {
        var model = CreateModel();

        Assert.Equal(
            [AppResources.EmptyChatExample1, AppResources.EmptyChatExample2, AppResources.EmptyChatExample3],
            model.Examples);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 300 && !condition(); attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private readonly CurrentSessionTracker _tracker = new();

    private SessionsPageModel CreateModel(TimeProvider? time = null)
    {
        var catalog = new ChatModelCatalogService(ProviderDoubles.Router(), NullLogger<ChatModelCatalogService>.Instance);
        var sessions = new SessionService(_sessionRepository, Substitute.For<IChatMessageRepository>(), _runs, _files, catalog, TimeProvider.System, Substitute.For<IConversationSummaryRepository>(), _changes, Substitute.For<ISessionGenerationCoordinator>());
        var apiKeys = new ApiKeyService(ProviderDoubles.Router(_provider), _secrets, NullLogger<ApiKeyService>.Instance);
        return new SessionsPageModel(CreateInitializer(), apiKeys, sessions, _navigation, _dialogs, time ?? TimeProvider.System, _tracker, new SessionSearchService(_searchTexts), _changes, _mainThread);
    }

    private static Session NewSession(string title, DateTimeOffset updatedAt) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        CreatedAt = updatedAt,
        UpdatedAt = updatedAt,
        Settings = SessionSettings.CreateDefault(),
    };

    private AppInitializer CreateInitializer()
    {
        var catalog = new ModelCatalogService(ProviderDoubles.Router(), Substitute.For<IImageModelRepository>(), NullLogger<ModelCatalogService>.Instance);
        var generation = new GenerationService(
            Substitute.For<ISessionRepository>(),
            _runs,
            ProviderDoubles.Router(Substitute.For<IModelProvider>()),
            Substitute.For<IImageFileStore>(),
            catalog,
            Substitute.For<ISeedSource>(),
            new AttachmentService(Substitute.For<IChatAttachmentRepository>(), Substitute.For<IImageFileStore>(), TimeProvider.System),
            TimeProvider.System,
            NullLogger<GenerationService>.Instance, new SessionChangeNotifier(NullLogger<SessionChangeNotifier>.Instance));
        return new AppInitializer(_database, generation, ProviderDoubles.BuiltInRegistry());
    }
}
