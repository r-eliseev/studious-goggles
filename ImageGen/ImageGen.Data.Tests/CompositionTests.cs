using ImageGen.Core;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Http;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ImageGen.Data.Tests;

public class CompositionTests
{
    [Fact]
    public void AddImageGenCoreAndData_BuildWithValidation_ResolvesAllServices()
    {
        using var provider = BuildProvider(NewDirectory());
        var router = provider.GetRequiredService<IProviderRouter>();

        var openRouter = Assert.IsType<InMemoryQueuedImageConnection>(router.GetConnection(ProviderIds.OpenRouter));
        Assert.Same(openRouter, router.GetImageProvider(ProviderIds.OpenRouter));
        Assert.Same(openRouter, router.GetChatClient(ProviderIds.OpenRouter));
        Assert.Same(openRouter, router.GetQueuedImageProvider(ProviderIds.OpenRouter));
        Assert.NotNull(provider.GetRequiredService<ISeedSource>());
        Assert.NotNull(provider.GetRequiredService<ApiKeyService>());
        Assert.NotNull(provider.GetRequiredService<ModelCatalogService>());
        Assert.NotNull(provider.GetRequiredService<SessionService>());
        Assert.NotNull(provider.GetRequiredService<GenerationService>());
        Assert.NotNull(provider.GetRequiredService<AssistantService>());
    }

    [Fact]
    public async Task AddImageGenCoreAndData_BuiltInTogetherResolvesImageCatalogWithoutKey()
    {
        using var provider = BuildProvider(NewDirectory());
        var router = provider.GetRequiredService<IProviderRouter>();
        var registry = provider.GetRequiredService<ProviderRegistry>();

        Assert.Equal([ProviderIds.OpenRouter, ProviderIds.Fal, ProviderIds.Together], registry.All.Select(item => item.Id));
        var connection = Assert.IsType<InMemoryQueuedImageConnection>(router.GetConnection(ProviderIds.Together));
        Assert.Same(connection, router.GetQueuedImageProvider(ProviderIds.Together));
        Assert.Same(connection, router.GetImageProvider(ProviderIds.Together));
        var models = await router.GetImageProvider(ProviderIds.Together)
            .ListImageModelsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            [
                "black-forest-labs/FLUX.1.1-pro",
                "black-forest-labs/FLUX.2-dev",
                "black-forest-labs/FLUX.2-flex",
                "black-forest-labs/FLUX.2-pro",
            ],
            models.Select(model => model.Id));
        Assert.All(models, model =>
        {
            Assert.Equal(ProviderIds.Together, model.ProviderId);
            Assert.False(model.SupportsImageInput);
            Assert.Empty(model.Prices);
        });
        Assert.Empty(await router.GetChatClient(ProviderIds.Together).ListChatModelsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AddImageGenCore_ServicesAreSingletons()
    {
        using var provider = BuildProvider(NewDirectory());

        Assert.Same(provider.GetRequiredService<GenerationService>(), provider.GetRequiredService<GenerationService>());
        Assert.Same(provider.GetRequiredService<GenerationService>(), provider.GetRequiredService<ISessionGenerationCoordinator>());
        Assert.Same(provider.GetRequiredService<AssistantService>(), provider.GetRequiredService<AssistantService>());
        Assert.Same(provider.GetRequiredService<SessionService>(), provider.GetRequiredService<SessionService>());
    }

    [Fact]
    public void FalTransport_DisablesAutomaticRedirectsAndHasOneLoggingHandler()
    {
        using var provider = BuildProvider(NewDirectory());
        using var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler("ImageGen.Internal.FalTransport");
        var current = handler;
        var delegatingHandlers = new List<DelegatingHandler>();
        while (current is DelegatingHandler delegating)
        {
            delegatingHandlers.Add(delegating);
            current = delegating.InnerHandler!;
        }

        var primary = Assert.IsType<HttpClientHandler>(current);

        Assert.False(primary.AllowAutoRedirect);
        Assert.Single(delegatingHandlers.OfType<LoggingHttpHandler>());
    }

    [Fact]
    public async Task DeleteSessionAsync_WaitsForStoppedGenerationBeforeDeletingRowsAndFiles()
    {
        var directory = NewDirectory();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ImageGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerClient = Substitute.For<IModelProvider>();
        var imageTransport = Substitute.For<ISynchronousImageTransport>();
        providerClient.ListImageModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ImageModel>>(
        [
            new ImageModel
            {
                ProviderId = ProviderIds.OpenRouter,
                Id = SessionSettings.DefaultImageModelId,
                Name = "Test model",
                SupportsSeed = true,
                SupportsImageInput = false,
                MaxImagesPerRequest = 0,
                FetchedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Parameters = [],
            },
        ]));
        imageTransport.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            entered.TrySetResult(call.Arg<CancellationToken>());
            return release.Task;
        });
        var router = Substitute.For<IProviderRouter>();
        router.ImageProviderIds.Returns([ProviderIds.OpenRouter]);
        router.GetImageProvider(ProviderIds.OpenRouter).Returns(providerClient);
        router.GetQueuedImageProvider(ProviderIds.OpenRouter).Returns(
            new InMemoryQueuedImageProvider(imageTransport, ProviderIds.OpenRouter, TimeProvider.System));

        using var provider = BuildProvider(directory, router);
        var cancellationToken = TestContext.Current.CancellationToken;
        await provider.GetRequiredService<SqliteDatabase>().InitializeAsync(cancellationToken);
        var sessionService = provider.GetRequiredService<SessionService>();
        var generationService = provider.GetRequiredService<GenerationService>();
        var sessions = provider.GetRequiredService<ISessionRepository>();
        var runs = provider.GetRequiredService<IRunRepository>();
        var files = provider.GetRequiredService<IImageFileStore>();
        var changes = provider.GetRequiredService<SessionChangeNotifier>();
        var session = await sessionService.CreateAsync(cancellationToken);
        await files.SaveAsync(session.Id, Guid.NewGuid(), [9, 8, 7], "image/png", cancellationToken);
        var folder = Path.Combine(directory, FileImageStore.ImagesFolder, session.Id.ToString("N"));
        var generationEvents = 0;
        generationService.GenerationUpdated += (_, _) => Interlocked.Increment(ref generationEvents);
        var sessionChanges = 0;
        changes.SessionChanged += (_, changedId) =>
        {
            if (changedId == session.Id)
            {
                Interlocked.Increment(ref sessionChanges);
            }
        };

        await generationService.StartRunAsync(session.Id, "wait for deletion", cancellationToken);
        var transportToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationRegistration = transportToken.Register(() => cancellationObserved.TrySetResult());
        var deleting = sessionService.DeleteAsync(session.Id, cancellationToken);
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        Assert.False(deleting.IsCompleted);
        Assert.NotNull(await sessions.GetAsync(session.Id, cancellationToken));
        Assert.Single(await runs.ListRunsBySessionAsync(session.Id, cancellationToken));
        Assert.Single(await runs.ListGenerationsBySessionAsync(session.Id, cancellationToken));
        Assert.True(Directory.Exists(folder));
        Assert.Equal(1, sessionChanges);

        release.SetResult(new ImageGenerationResult([1, 2, 3], "image/png", 0.014m));
        await deleting.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        await generationService.WhenIdleAsync();
        var eventsAfterDelete = Volatile.Read(ref generationEvents);

        Assert.Null(await sessions.GetAsync(session.Id, cancellationToken));
        Assert.Empty(await runs.ListRunsBySessionAsync(session.Id, cancellationToken));
        Assert.Empty(await runs.ListGenerationsBySessionAsync(session.Id, cancellationToken));
        Assert.False(Directory.Exists(folder));
        Assert.Equal(2, sessionChanges);
        Assert.Equal(eventsAfterDelete, Volatile.Read(ref generationEvents));
    }

    [Fact]
    public void UserConfigFile_AddsProviderToRegistry()
    {
        var directory = NewDirectory();
        File.WriteAllText(
            Path.Combine(directory, ProviderConfigLoader.UserFileName),
            """{"providers":[{"id":"local","kind":"openAiCompatible","displayName":"Local","baseAddress":"http://localhost:1234/v1","timeoutSeconds":60}]}""");
        using var provider = BuildProvider(directory);

        var registry = provider.GetRequiredService<ProviderRegistry>();

        Assert.Equal(["openrouter", "fal", "together", "local"], registry.All.Select(p => p.Id));
    }

    [Fact]
    public void InvalidUserConfigFile_ThrowsWhenRegistryIsResolved()
    {
        var directory = NewDirectory();
        File.WriteAllText(Path.Combine(directory, ProviderConfigLoader.UserFileName), """{"providers":[{"id":"x","kind":"carrier"}]}""");
        using var provider = BuildProvider(directory);

        var ex = Assert.Throws<ProviderConfigException>(() => provider.GetRequiredService<ProviderRegistry>());

        Assert.Contains("carrier", ex.Message);
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static ServiceProvider BuildProvider(string dataDirectory, IProviderRouter? router = null)
    {
        var paths = Substitute.For<IAppPaths>();
        paths.DataDirectory.Returns(dataDirectory);
        var secrets = Substitute.For<ISecretStore>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(paths);
        services.AddSingleton(secrets);
        var defaultAssistant = Substitute.For<IDefaultAssistantModel>();
        defaultAssistant.Current.Returns(new ModelRef(ProviderIds.OpenRouter, SessionSettings.DefaultAssistantModelId));
        services.AddSingleton(defaultAssistant);
        services.AddImageGenCore();
        if (router is not null)
        {
            services.RemoveAll<IProviderRouter>();
            services.AddSingleton(router);
        }
        services.AddImageGenData();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
