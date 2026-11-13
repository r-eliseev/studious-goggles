using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Data;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Tests.Services;

public class AppInitializerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
    private readonly SqliteDatabase _database;
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();

    public AppInitializerTests()
    {
        _database = new SqliteDatabase(Path.Combine(_directory, SqliteDatabase.FileName));
        _runs.MarkUnfinishedAsInterruptedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));
    }

    [Fact]
    public async Task EnsureInitializedAsync_FirstCall_CreatesDatabaseBeforeRecovery()
    {
        var databaseExistedDuringRecovery = false;
        _runs.MarkUnfinishedAsInterruptedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                databaseExistedDuringRecovery = File.Exists(_database.DatabasePath);
                return Task.FromResult(0);
            });
        var initializer = CreateInitializer();

        await initializer.EnsureInitializedAsync();

        Assert.True(databaseExistedDuringRecovery);
        await _runs.Received(1).MarkUnfinishedAsInterruptedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureInitializedAsync_DeletesOldAtomicWriteTemporaryFiles()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(_directory);
        var files = new FileImageStore(paths, NullLogger<FileImageStore>.Instance);
        var temporaryPath = Path.Combine(_directory, "images", "s1", ".image.png.0123456789abcdef0123456789abcdef.tmp");
        Directory.CreateDirectory(Path.GetDirectoryName(temporaryPath)!);
        await File.WriteAllTextAsync(temporaryPath, "temporary", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(temporaryPath, DateTime.UtcNow.AddHours(-2));
        var initializer = CreateInitializer(files);

        await initializer.EnsureInitializedAsync();

        Assert.False(File.Exists(temporaryPath));
    }

    [Fact]
    public async Task EnsureInitializedAsync_CalledTwice_RunsStartupOnce()
    {
        var initializer = CreateInitializer();

        await initializer.EnsureInitializedAsync();
        await initializer.EnsureInitializedAsync();

        await _runs.Received(1).MarkUnfinishedAsInterruptedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureInitializedAsync_ConcurrentCalls_RunsStartupOnceAndAllAwaitIt()
    {
        var initializer = CreateInitializer();

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => initializer.EnsureInitializedAsync()));

        await _runs.Received(1).MarkUnfinishedAsInterruptedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureInitializedAsync_DoesNotWaitForModelCatalogRefresh()
    {
        var enteredRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refresh = Substitute.For<Func<CancellationToken, Task>>();
        refresh(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            enteredRefresh.SetResult();
            await releaseRefresh.Task;
        });
        var initializer = CreateInitializer(refreshModelCatalogs: refresh);

        await initializer.EnsureInitializedAsync();
        await enteredRefresh.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        _ = refresh.Received(1).Invoke(Arg.Any<CancellationToken>());
        releaseRefresh.SetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private AppInitializer CreateInitializer(IImageFileStore? files = null, Func<CancellationToken, Task>? refreshModelCatalogs = null)
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
        return new AppInitializer(_database, generation, ProviderDoubles.BuiltInRegistry(), files ?? Substitute.For<IImageFileStore>(), TimeProvider.System,
            refreshModelCatalogs ?? (_ => Task.CompletedTask));
    }
}
