using Avalonia.Controls;
using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class GallerySteps(ScenarioContext scenario)
{
    private AppWorld World => scenario.Get<AppWorld>();

    [Given("the user has gallery images and failed generations across two sessions")]
    public Task GivenGalleryImagesAndFailuresAcrossTwoSessions() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.Secrets.SetAsync(ImageGen.Core.Domain.Keys.SecretNames.OpenRouterApiKey, "e2e-mock-key", cancellationToken);
        await World.StartAsync(cancellationToken);
        var sessions = World.Provider.GetRequiredService<ISessionRepository>();
        var runs = World.Provider.GetRequiredService<IRunRepository>();
        var files = World.Provider.GetRequiredService<IImageFileStore>();
        var image = await File.ReadAllBytesAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Assets/reference.png")), cancellationToken);
        var now = World.Time.GetUtcNow();
        var expectedPrompts = new List<string>();

        foreach (var (title, prompt, status, minutesAgo) in new[]
        {
            ("First session", "older success", GenerationStatus.Succeeded, 10),
            ("First session", "failed image", GenerationStatus.Failed, 5),
            ("Second session", "newer success", GenerationStatus.Succeeded, 1),
        })
        {
            var sessionId = Guid.NewGuid();
            var at = now.AddMinutes(-minutesAgo);
            await sessions.AddAsync(new Session
            {
                Id = sessionId,
                Title = title,
                CreatedAt = at,
                UpdatedAt = at,
                Settings = SessionSettings.CreateDefault(),
            }, cancellationToken);
            var runId = Guid.NewGuid();
            var generationId = Guid.NewGuid();
            var generation = new Generation
            {
                Id = generationId,
                RunId = runId,
                SessionId = sessionId,
                ProviderId = "openrouter",
                ModelId = "vendor/gallery",
                Prompt = prompt,
                Seed = 1,
                SeedApplied = true,
                Status = status,
                ImagePath = status == GenerationStatus.Succeeded
                    ? await files.SaveAsync(sessionId, generationId, image, "image/png", cancellationToken)
                    : null,
                MediaType = status == GenerationStatus.Succeeded ? "image/png" : null,
                CreatedAt = at,
                CompletedAt = at,
                SortOrder = 0,
            };
            var run = new Run
            {
                Id = runId,
                SessionId = sessionId,
                Prompt = prompt,
                Origin = RunOrigin.Assistant,
                CreatedAt = at,
            };
            await runs.AddRunAsync(run, [generation], cancellationToken);
            if (status == GenerationStatus.Succeeded) expectedPrompts.Add(prompt);
        }

        scenario.Set(expectedPrompts, "gallery.expected-prompts");
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user opens the gallery")]
    public Task WhenUserOpensGallery() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var navigation = World.Provider.GetRequiredService<AvaloniaNavigationService>();
        await navigation.GoToAsync(Routes.Gallery);
        var model = Assert.IsType<GalleryPageModel>(Assert.IsType<GalleryView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => !model.IsBusy && model.Items.Count > 0, "gallery items to load", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the gallery shows only the successful images, newest first")]
    public Task ThenGalleryShowsSuccessfulImagesNewestFirst() => HeadlessApp.RunAsync(_ =>
    {
        var model = Assert.IsType<GalleryPageModel>(Assert.IsType<GalleryView>(World.Window.PageArea.Content).DataContext);
        var expected = scenario.Get<List<string>>("gallery.expected-prompts");
        Assert.Equal(expected.OrderByDescending(prompt => prompt == "newer success"), model.Items.Select(item => item.Prompt));
        Assert.DoesNotContain(model.Items, item => item.Prompt == "failed image");
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Given("the user has gallery images from different models and dates")]
    public Task GivenGalleryImagesFromDifferentModelsAndDates() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.Secrets.SetAsync(ImageGen.Core.Domain.Keys.SecretNames.OpenRouterApiKey, "e2e-mock-key", cancellationToken);
        await World.StartAsync(cancellationToken);
        foreach (var item in new[]
        {
            ("vendor/fox", "recent fox", 2),
            ("vendor/fox", "older fox", 20),
            ("vendor/owl", "recent owl", 1),
            ("vendor/fox", "old fox", 40),
        })
        {
            await AddGalleryImageAsync(item.Item1, item.Item2, World.Time.GetUtcNow().AddDays(-item.Item3), cancellationToken);
        }
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user opens the gallery and filters model {string} for the last {int} days")]
    public Task WhenUserOpensGalleryAndFilters(string modelId, int days) => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var navigation = World.Provider.GetRequiredService<AvaloniaNavigationService>();
        await navigation.GoToAsync(Routes.Gallery);
        var model = Assert.IsType<GalleryPageModel>(Assert.IsType<GalleryView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => !model.IsBusy && model.Models.Count > 0, "gallery filters to load", cancellationToken);
        model.SelectedModel = Assert.Single(model.Models, option => option.Reference?.ModelId == modelId);
        model.SelectedPeriod = days == 7 ? GalleryPeriod.Last7Days : GalleryPeriod.Last30Days;
        await model.ApplyFiltersCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user changes the gallery period to the last {int} days")]
    public Task WhenUserChangesGalleryPeriod(int days) => HeadlessApp.RunAsync(async _ =>
    {
        var model = Assert.IsType<GalleryPageModel>(Assert.IsType<GalleryView>(World.Window.PageArea.Content).DataContext);
        model.SelectedPeriod = days == 7 ? GalleryPeriod.Last7Days : GalleryPeriod.Last30Days;
        await model.ApplyFiltersCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the gallery shows only images for model {string} from the last {int} days")]
    public Task ThenGalleryShowsModelAndPeriod(string modelId, int days) => HeadlessApp.RunAsync(_ =>
    {
        var model = Assert.IsType<GalleryPageModel>(Assert.IsType<GalleryView>(World.Window.PageArea.Content).DataContext);
        Assert.All(model.Items, item => Assert.Contains(modelId, item.ModelText, StringComparison.Ordinal));
        var expected = days == 7 ? new[] { "recent fox" } : new[] { "recent fox", "older fox" };
        Assert.Equal(expected, model.Items.Select(item => item.Prompt));
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Given("the user has an image in a gallery session")]
    public Task GivenImageInGallerySession() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.Secrets.SetAsync(ImageGen.Core.Domain.Keys.SecretNames.OpenRouterApiKey, "e2e-mock-key", cancellationToken);
        await World.StartAsync(cancellationToken);
        await AddGalleryImageAsync("vendor/fox", "source session image", World.Time.GetUtcNow(), cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user opens that image from the gallery")]
    public Task WhenUserOpensImageFromGallery() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.Provider.GetRequiredService<AvaloniaNavigationService>().GoToAsync(Routes.Gallery);
        var gallery = Assert.IsType<GalleryPageModel>(Assert.IsType<GalleryView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => !gallery.IsBusy && gallery.Items.Count == 1, "the gallery image to load", cancellationToken);
        scenario.Set(gallery.Items[0].SessionId, "gallery.source-session-id");
        await gallery.OpenCommand.ExecuteAsync(gallery.Items[0]);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the source session chat is open")]
    public Task ThenSourceSessionChatIsOpen() => HeadlessApp.RunAsync(_ =>
    {
        var navigation = World.Provider.GetRequiredService<AvaloniaNavigationService>();
        Assert.Equal(Routes.ChatFor(scenario.Get<Guid>("gallery.source-session-id")), navigation.CurrentRoute);
        Assert.IsType<ChatView>(World.Window.PageArea.Content);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Given("a session has a run with two images and another run with an image")]
    public Task GivenRunWithTwoImagesAndAnotherRun() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.Secrets.SetAsync(ImageGen.Core.Domain.Keys.SecretNames.OpenRouterApiKey, "e2e-mock-key", cancellationToken);
        await World.StartAsync(cancellationToken);
        var sessionId = await CreateSessionAsync(cancellationToken);
        var firstRun = await AddRunAsync(sessionId, ["first image", "second image"], cancellationToken);
        await AddRunAsync(sessionId, ["other run image"], cancellationToken);
        scenario.Set(sessionId, "gallery.viewer-session-id");
        scenario.Set(firstRun, "gallery.viewer-run-id");
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user opens the second image in the first run")]
    public Task WhenUserOpensSecondImageInFirstRun() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var sessionId = scenario.Get<Guid>("gallery.viewer-session-id");
        await World.Provider.GetRequiredService<AvaloniaNavigationService>().GoToAsync(Routes.ChatFor(sessionId));
        var chat = Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => chat.Items.OfType<RunItemViewModel>().Any(run => run.Id == scenario.Get<Guid>("gallery.viewer-run-id")), "the first run to load", cancellationToken);
        var run = Assert.Single(chat.Items.OfType<RunItemViewModel>(), item => item.Id == scenario.Get<Guid>("gallery.viewer-run-id"));
        var selected = Assert.Single(run.Generations, card => card.Prompt == "second image");
        scenario.Set(run.Generations.Select(card => card.Id).ToArray(), "gallery.viewer-image-ids");
        await chat.OpenImageCommand.ExecuteAsync(selected);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the viewer contains only the first run images and starts at the selected image")]
    public Task ThenViewerContainsFirstRunImages() => HeadlessApp.RunAsync(_ =>
    {
        var expectedIds = scenario.Get<Guid[]>("gallery.viewer-image-ids");
        Assert.Equal(expectedIds, World.Platform.ViewerItems.Select(item => item.Id));
        Assert.Equal(1, World.Platform.ViewerInitialIndex);
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [Then("viewer paging stops at the first and last image")]
    public void ThenViewerPagingStopsAtFirstAndLastImage()
    {
        var state = new ImageGen.Presentation.Services.ImageViewerNavigationState(World.Platform.ViewerItems.Count, World.Platform.ViewerInitialIndex);
        Assert.False(state.TryNavigate(1, out _));
        Assert.True(state.TryNavigate(-1, out var first));
        Assert.Equal(0, first.Index);
        Assert.False(state.TryNavigate(-1, out _));
    }

    [Given("a session has two successful images to compare")]
    public Task GivenTwoImagesToCompare() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await World.Secrets.SetAsync(ImageGen.Core.Domain.Keys.SecretNames.OpenRouterApiKey, "e2e-mock-key", cancellationToken);
        await World.StartAsync(cancellationToken);
        var sessionId = await CreateSessionAsync(cancellationToken);
        var runId = await AddRunAsync(sessionId, ["first compare image", "second compare image"], cancellationToken);
        scenario.Set(sessionId, "gallery.compare-session-id");
        scenario.Set(runId, "gallery.compare-run-id");
        return true;
    }, TestContext.Current.CancellationToken);

    [When("the user compares the first image with the second")]
    public Task WhenUserComparesImages() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var sessionId = scenario.Get<Guid>("gallery.compare-session-id");
        await World.Provider.GetRequiredService<AvaloniaNavigationService>().GoToAsync(Routes.ChatFor(sessionId));
        var chat = Assert.IsType<ChatPageModel>(Assert.IsType<ChatView>(World.Window.PageArea.Content).DataContext);
        await World.WaitUntilAsync(() => chat.Items.OfType<RunItemViewModel>().Any(run => run.Id == scenario.Get<Guid>("gallery.compare-run-id")), "the comparison run to load", cancellationToken);
        var run = Assert.Single(chat.Items.OfType<RunItemViewModel>(), item => item.Id == scenario.Get<Guid>("gallery.compare-run-id"));
        var first = Assert.Single(run.Generations, card => card.Prompt == "first compare image");
        World.Platform.QueueFirstChoice();
        await first.CompareCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("comparison opens in side-by-side mode")]
    public void ThenComparisonOpensSideBySide() => Assert.Equal(ComparisonMode.SideBySide, Assert.IsType<ComparisonPageModel>(World.Platform.Comparison).Mode);

    [When("the user switches comparison to overlay mode and sets zoom to {int}")]
    public void WhenUserSwitchesComparisonModeAndZoom(int zoom)
    {
        var comparison = Assert.IsType<ComparisonPageModel>(World.Platform.Comparison);
        comparison.Mode = ComparisonMode.Overlay;
        comparison.SetZoom(zoom);
    }

    [Then("both comparison images use zoom {int}")]
    public void ThenBothComparisonImagesUseSharedZoom(int zoom)
    {
        var comparison = Assert.IsType<ComparisonPageModel>(World.Platform.Comparison);
        Assert.Equal(ComparisonMode.Overlay, comparison.Mode);
        Assert.Equal(zoom, comparison.Zoom);
    }

    private async Task<Guid> CreateSessionAsync(CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        var now = World.Time.GetUtcNow();
        await World.Provider.GetRequiredService<ISessionRepository>().AddAsync(new Session
        {
            Id = sessionId,
            Title = "Gallery test session",
            CreatedAt = now,
            UpdatedAt = now,
            Settings = SessionSettings.CreateDefault(),
        }, cancellationToken);
        return sessionId;
    }

    private async Task<Guid> AddRunAsync(Guid sessionId, string[] prompts, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid();
        var at = World.Time.GetUtcNow();
        var image = await File.ReadAllBytesAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Assets/reference.png")), cancellationToken);
        var store = World.Provider.GetRequiredService<IImageFileStore>();
        var generations = new List<Generation>();
        for (var index = 0; index < prompts.Length; index++)
        {
            var generationId = Guid.NewGuid();
            var path = await store.SaveAsync(sessionId, generationId, image, "image/png", cancellationToken);
            generations.Add(new Generation
            {
                Id = generationId,
                RunId = runId,
                SessionId = sessionId,
                ProviderId = "openrouter",
                ModelId = "vendor/compare",
                Prompt = prompts[index],
                Seed = index + 1,
                SeedApplied = true,
                Status = GenerationStatus.Succeeded,
                ImagePath = path,
                MediaType = "image/png",
                CreatedAt = at,
                CompletedAt = at,
                SortOrder = index,
                CostUsd = 0.01m,
            });
        }
        await World.Provider.GetRequiredService<IRunRepository>().AddRunAsync(new Run
        {
            Id = runId,
            SessionId = sessionId,
            Prompt = prompts[0],
            Origin = RunOrigin.Assistant,
            CreatedAt = at,
        }, generations, cancellationToken);
        return runId;
    }

    private async Task AddGalleryImageAsync(string modelId, string prompt, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        await World.Provider.GetRequiredService<ISessionRepository>().AddAsync(new Session
        {
            Id = sessionId,
            Title = "Gallery session",
            CreatedAt = at,
            UpdatedAt = at,
            Settings = SessionSettings.CreateDefault(),
        }, cancellationToken);
        var runId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var image = await File.ReadAllBytesAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Assets/reference.png")), cancellationToken);
        var imagePath = await World.Provider.GetRequiredService<IImageFileStore>().SaveAsync(sessionId, generationId, image, "image/png", cancellationToken);
        var generation = new Generation
        {
            Id = generationId,
            RunId = runId,
            SessionId = sessionId,
            ProviderId = "openrouter",
            ModelId = modelId,
            Prompt = prompt,
            Seed = 1,
            SeedApplied = true,
            Status = GenerationStatus.Succeeded,
            ImagePath = imagePath,
            MediaType = "image/png",
            CreatedAt = at,
            CompletedAt = at,
            SortOrder = 0,
        };
        await World.Provider.GetRequiredService<IRunRepository>().AddRunAsync(new Run
        {
            Id = runId,
            SessionId = sessionId,
            Prompt = prompt,
            Origin = RunOrigin.Assistant,
            CreatedAt = at,
        }, [generation], cancellationToken);
    }
}
