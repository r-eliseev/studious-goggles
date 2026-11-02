using ImageGen.Core.Domain.Gallery;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Platform;
using ImageGen.Core.Storage;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Tests.PageModels;

public sealed class GalleryPageModelTests
{
    [Fact]
    public async Task ApplyFiltersAsync_IgnoresOldResponseAfterFilterChanges()
    {
        var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var gallery = Substitute.For<IGalleryRepository>();
        gallery.ListModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ModelRef>>([]));
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResult = new TaskCompletionSource<GalleryPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldItem = Item("old");
        var currentItem = Item("current");
        gallery.ListAsync(Arg.Any<GalleryQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var query = call.Arg<GalleryQuery>();
            if (query.From is null)
            {
                firstStarted.TrySetResult();
                return firstResult.Task;
            }
            return Task.FromResult(new GalleryPage([currentItem], null));
        });
        var model = CreateModel(gallery, now).Model;

        var oldLoad = model.LoadCommand.ExecuteAsync(null);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        model.SelectedPeriod = GalleryPeriod.Last7Days;
        await model.ApplyFiltersCommand.ExecuteAsync(null);
        firstResult.SetResult(new GalleryPage([oldItem], null));
        await oldLoad;

        Assert.Single(model.Items);
        Assert.Equal("current", model.Items[0].Prompt);
    }

    [Fact]
    public async Task LoadMoreCommand_UsesRepositoryCursorAndAppendsNextPage()
    {
        var gallery = Substitute.For<IGalleryRepository>();
        gallery.ListModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ModelRef>>([]));
        var cursor = new GalleryCursor(DateTimeOffset.UnixEpoch, Guid.NewGuid());
        gallery.ListAsync(Arg.Any<GalleryQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var query = call.Arg<GalleryQuery>();
            return Task.FromResult(query.Cursor is null
                ? new GalleryPage([Item("first")], cursor)
                : new GalleryPage([Item("second")], null));
        });
        var model = CreateModel(gallery, DateTimeOffset.UnixEpoch).Model;

        await model.LoadCommand.ExecuteAsync(null);
        await model.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(["first", "second"], model.Items.Select(item => item.Prompt));
        Assert.False(model.HasMore);
        await gallery.Received(1).ListAsync(Arg.Is<GalleryQuery>(query => query.Cursor == cursor), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenCommand_RemovedSession_MarksItemUnavailableAndExplainsWhy()
    {
        var gallery = Substitute.For<IGalleryRepository>();
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ImageGen.Core.Domain.Sessions.Session?)null);
        var files = Substitute.For<IImageFileStore>();
        files.GetFullPath("images/test.png").Returns("/missing/test.png");
        var fixture = CreateModel(gallery, DateTimeOffset.UnixEpoch, sessions, files);
        var model = fixture.Model;
        var item = new GalleryItemViewModel(Item("prompt"), "/missing/test.png");

        await model.OpenCommand.ExecuteAsync(item);

        Assert.True(item.IsUnavailable);
        Assert.Equal(AppResources.GalleryUnavailableMessage, item.StatusText);
        await fixture.Dialogs.Received(1).AlertAsync(AppResources.GalleryTitle, AppResources.GalleryUnavailableMessage);
    }

    [Fact]
    public async Task OpenCommand_ExistingSessionAndFile_OpensChat()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gallery-page-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var imagePath = Path.Combine(directory, "test.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3], TestContext.Current.CancellationToken);
        try
        {
            var gallery = Substitute.For<IGalleryRepository>();
            var sessions = Substitute.For<ISessionRepository>();
            var sessionId = Guid.NewGuid();
            sessions.GetAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new ImageGen.Core.Domain.Sessions.Session
            {
                Id = sessionId,
                Title = "session",
                CreatedAt = DateTimeOffset.UnixEpoch,
                UpdatedAt = DateTimeOffset.UnixEpoch,
                Settings = ImageGen.Core.Domain.Sessions.SessionSettings.CreateDefault(),
            });
            var files = Substitute.For<IImageFileStore>();
            files.GetFullPath("images/test.png").Returns(imagePath);
            var navigation = Substitute.For<INavigationService>();
            var fixture = CreateModel(gallery, DateTimeOffset.UnixEpoch, sessions, files, navigation);
            var item = new GalleryItemViewModel(Item("prompt") with { SessionId = sessionId }, imagePath);

            await fixture.Model.OpenCommand.ExecuteAsync(item);

            await navigation.Received(1).GoToAsync(Routes.ChatFor(sessionId));
            Assert.False(item.IsUnavailable);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (GalleryPageModel Model, IDialogService Dialogs) CreateModel(IGalleryRepository gallery, DateTimeOffset now,
        ISessionRepository? sessions = null, IImageFileStore? files = null, INavigationService? navigation = null)
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(now);
        var dialogs = Substitute.For<IDialogService>();
        return (new GalleryPageModel(gallery, sessions ?? Substitute.For<ISessionRepository>(), files ?? Substitute.For<IImageFileStore>(),
            navigation ?? Substitute.For<INavigationService>(), dialogs, time, TimeZoneInfo.Utc), dialogs);
    }

    private static GalleryItem Item(string prompt) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "session", new ModelRef("openrouter", "image"), prompt,
        "images/test.png", "image/png", 0.1m, DateTimeOffset.UnixEpoch);
}
