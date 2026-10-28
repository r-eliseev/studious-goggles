using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
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

    public SessionsPageModelTests()
    {
        _database = new SqliteDatabase(Path.Combine(_directory, SqliteDatabase.FileName));
        _runs.MarkUnfinishedAsInterruptedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns("sk-or-saved");
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
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

        await _navigation.Received(1).GoToAsync(Routes.ApiKey);
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
        _dialogs.ConfirmAsync(AppResources.DeleteSessionTitle, AppResources.DeleteSessionMessage, AppResources.DeleteSession, AppResources.Cancel)
            .Returns(true);
        var model = CreateModel();
        await model.OnAppearingAsync();

        await model.Sessions[0].DeleteCommand.ExecuteAsync(null);

        await _sessionRepository.Received(1).DeleteAsync(session.Id, Arg.Any<CancellationToken>());
        await _files.Received(1).DeleteSessionFilesAsync(session.Id, Arg.Any<CancellationToken>());
        Assert.Empty(model.Sessions);
    }

    [Fact]
    public async Task DeleteSession_Cancelled_KeepsSessionAndRow()
    {
        var session = NewSession("Keep me", DateTimeOffset.UtcNow);
        _sessionRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([session]);
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var model = CreateModel();
        await model.OnAppearingAsync();

        await model.Sessions[0].DeleteCommand.ExecuteAsync(null);

        await _sessionRepository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Assert.Single(model.Sessions);
    }

    private SessionsPageModel CreateModel()
    {
        var sessions = new SessionService(_sessionRepository, Substitute.For<IChatMessageRepository>(), _runs, _files, TimeProvider.System);
        var apiKeys = new ApiKeyService(ProviderDoubles.Router(_provider), _secrets, NullLogger<ApiKeyService>.Instance);
        return new SessionsPageModel(CreateInitializer(), apiKeys, sessions, _navigation, _dialogs);
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
            TimeProvider.System,
            NullLogger<GenerationService>.Instance);
        return new AppInitializer(_database, generation, ProviderDoubles.BuiltInRegistry());
    }
}
