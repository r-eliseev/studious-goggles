using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Fal;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace ImageGen.Acceptance.Tests.Support;

public sealed class AppWorld : IDisposable
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);
    private readonly WorldAppPaths _paths;
    private ServiceProvider? _provider;
    private MainWindow? _window;
    private GenerationService? _generations;
    private IImageFileStore? _imageFiles;
    private AvaloniaAppearanceApplier? _appearanceApplier;

    public AppWorld()
    {
        var dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "tmp", "acceptance-world-" + Guid.NewGuid().ToString("N"));
        _paths = new WorldAppPaths(dataDirectory);
        Secrets = new DesktopSecretStore(backend: null);
        Network = new FakeNetwork();
        Network.RegisterSecret("e2e-mock-key");
        OpenRouter = new FakeOpenRouter(Network);
        OpenRouter.SetImageModels(
        [
            new FakeOpenRouter.ImageModel("black-forest-labs/flux.2-klein-4b", "FLUX.2 Klein 4B", SupportsSeed: true, PricePerMegapixel: 0.014m),
            new FakeOpenRouter.ImageModel("vendor/beta", "Beta Pro", SupportsSeed: false, PricePerMegapixel: null),
        ]);
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        Seeds = new SequenceSeedSource();
        Platform = new FakePlatform();
    }

    public DesktopSecretStore Secrets { get; }

    public FakeNetwork Network { get; }

    public FakeOpenRouter OpenRouter { get; }

    public FakeTimeProvider Time { get; }

    public SequenceSeedSource Seeds { get; }

    public FakePlatform Platform { get; }

    public string DataDirectory => _paths.DataDirectory;

    public async Task ConfigureChatProviderAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DataDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(DataDirectory, "providers.json"),
            """{"providers":[{"id":"e2e-mock","kind":"openAiCompatible","displayName":"E2E Mock","baseAddress":"https://localhost:5443/v1/","chatModels":[{"id":"e2e/assistant","name":"E2E Assistant","supportsImageInput":false,"contextLength":8192}]}]}""",
            cancellationToken);
        Network.AddResponse(HttpMethod.Get, "localhost", "/v1/models", System.Net.HttpStatusCode.OK, "{}");
    }

    public ServiceProvider Provider => _provider ?? throw new InvalidOperationException("The app world has not been started.");

    public MainWindow Window => _window ?? throw new InvalidOperationException("The app world has not been started.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_provider is not null) throw new InvalidOperationException("The app world is already started.");
        _provider = AppServices.Create(
            _paths,
            Secrets,
            Secrets,
            configureLogging: logging => logging.AddImageGenFileLogging(Path.Combine(DataDirectory, "logs"), LogLevel.Debug),
            dialogs: Platform,
            configureTestServices: ConfigureTestServices);
        _appearanceApplier = new AvaloniaAppearanceApplier(
            _provider.GetRequiredService<AppearanceService>(),
            Application.Current!);
        _appearanceApplier.Start();
        _window = new MainWindow();
        var navigation = _provider.GetRequiredService<AvaloniaNavigationService>();
        navigation.AttachHost(_window.PageArea);
        _window.ConfigureShortcuts(_provider.GetRequiredService<SessionsPageModel>(), navigation);
        _generations = _provider.GetRequiredService<GenerationService>();
        _imageFiles = _provider.GetRequiredService<IImageFileStore>();
        var hasOpenRouterKey = !string.IsNullOrWhiteSpace(await Secrets.GetAsync(SecretNames.OpenRouterApiKey, cancellationToken));
        await navigation.GoToAsync(Routes.Sessions);
        var expectedRoute = hasOpenRouterKey ? Routes.Sessions : Routes.ApiKeyFor(ProviderIds.OpenRouter, firstRun: true);
        await WaitUntilAsync(() => navigation.CurrentRoute == expectedRoute, $"the initial route '{expectedRoute}'", cancellationToken);
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        await DrainBackgroundWorkAsync(cancellationToken);
        await AdvanceTestTimeAndDrainAsync();
        await StopCompositionAsync();
        await StartAsync(cancellationToken);
    }

    public Task WaitUntilAsync(Func<bool> condition, string description, CancellationToken cancellationToken) =>
        WaitUntilCoreAsync(condition, description, cancellationToken);

    public void Dispose()
        => DisposeAsync().GetAwaiter().GetResult();

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await DrainBackgroundWorkAsync(CancellationToken.None);
            await AdvanceTestTimeAndDrainAsync();
            await StopCompositionAsync();
        }

        Network.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(DataDirectory)) Directory.Delete(DataDirectory, recursive: true);
    }

    private void ConfigureTestServices(IServiceCollection services)
    {
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => Network));
        services.AddHttpClient(FalImageProvider.TransportName).ConfigurePrimaryHttpMessageHandler(() => Network);
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton<ISeedSource>(Seeds);
        services.AddSingleton<IClipboardService>(Platform);
        services.AddSingleton<IDialogService>(Platform);
        services.AddSingleton<IImageSaver>(Platform);
        services.AddSingleton<IFilePickerService>(Platform);
    }

    private async Task DrainBackgroundWorkAsync(CancellationToken cancellationToken)
    {
        if (_generations is not null)
        {
            await WaitForTaskAsync(_generations.WhenIdleAsync(), "background generations", cancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
        if (_imageFiles is not null)
        {
            await WaitForTaskAsync(_imageFiles.WhenIdleAsync(), "image file operations", cancellationToken);
        }
    }

    private async Task AdvanceTestTimeAndDrainAsync()
    {
        Time.Advance(TimeSpan.FromDays(1));
        await WaitForTaskAsync(Task.Delay(TimeSpan.FromMilliseconds(50)), "fake-time callbacks", CancellationToken.None);
    }

    private static async Task WaitForTaskAsync(Task task, string description, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(OperationTimeout);
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, timeout.Token);
        }

        await task;
        cancellationToken.ThrowIfCancellationRequested();
        if (timeout.IsCancellationRequested) throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private async Task WaitUntilCoreAsync(Func<bool> condition, string description, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(OperationTimeout);
        while (!condition())
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, timeout.Token);
        }
    }

    private async Task StopCompositionAsync()
    {
        _window?.Close();
        var provider = _provider;
        _provider = null;
        if (provider is not null)
        {
            await WaitForTaskAsync(Task.Run(async () => await provider.DisposeAsync()), "application services to stop", CancellationToken.None);
        }

        _window = null;
        _appearanceApplier = null;
        _generations = null;
        _imageFiles = null;
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class WorldAppPaths(string dataDirectory) : IAppPaths
    {
        public string DataDirectory { get; } = dataDirectory;
    }
}
