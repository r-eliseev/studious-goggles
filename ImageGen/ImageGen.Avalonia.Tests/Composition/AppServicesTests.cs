using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Composition;

public class AppServicesTests
{
    [Fact]
    public void ApiKeyRoute_ForwardsProviderQueryToPageModel()
    {
        var secrets = new DesktopSecretStore(backend: null);
        using var provider = AppServices.Create(new FixedAppPaths(), secrets, secrets);
        var create = provider.GetRequiredService<PageRoutes>().Find(Routes.ApiKey)!;

        var view = Assert.IsType<ApiKeyView>(create(new Dictionary<string, string>
        {
            [Routes.ProviderIdParameter] = "fal",
        }));

        var model = Assert.IsType<ApiKeyPageModel>(view.DataContext);
        Assert.Equal("fal", model.ProviderId);
        Assert.Equal("fal.ai", model.ProviderDisplayName);
    }

    [Fact]
    public void Create_ValidateOnBuild_ResolvesPageModels()
    {
        var secrets = new DesktopSecretStore(backend: null);
        using var provider = AppServices.Create(new FixedAppPaths(), secrets, secrets);

        Assert.NotNull(provider.GetRequiredService<SessionsPageModel>());
        Assert.NotNull(provider.GetRequiredService<ChatPageModel>());
        Assert.IsType<ImageGen.Avalonia.Services.AvaloniaImageViewerService>(provider.GetRequiredService<ImageGen.Presentation.Services.IImageViewerService>());
        Assert.IsType<ImageGen.Avalonia.Services.AvaloniaImageComparisonService>(provider.GetRequiredService<ImageGen.Presentation.Services.IImageComparisonService>());
        Assert.NotNull(provider.GetRequiredService<ApiKeyPageModel>());
    }

    [Fact]
    public void CreateShared_WithPlatformServicesFromShell_BuildsAndResolvesPages()
    {
        var secrets = new DesktopSecretStore(backend: null);
        using var provider = AppServices.CreateShared(new FixedAppPaths(), secrets, secrets, registerPlatform: services =>
        {
            services.AddSingleton(Substitute.For<IDialogService>());
            services.AddSingleton(Substitute.For<IFilePickerService>());
            services.AddSingleton(Substitute.For<IClipboardService>());
            services.AddSingleton(Substitute.For<IImageSaver>());
            services.AddSingleton(Substitute.For<IImageViewerService>());
            services.AddSingleton(Substitute.For<IImageComparisonService>());
            services.AddSingleton(Substitute.For<IFolderLauncher>());
            services.AddSingleton(Substitute.For<ILogSharer>());
            services.AddSingleton(new AvaloniaFullScreenController());
        });

        Assert.NotNull(provider.GetRequiredService<SessionsPageModel>());
        Assert.NotNull(provider.GetRequiredService<ChatPageModel>());
        Assert.NotNull(provider.GetRequiredService<SettingsPageModel>());
        Assert.NotNull(provider.GetRequiredService<PageRoutes>());
    }

    [Fact]
    public void Create_ConfigureTestServices_ReplacesPlatformRegistration()
    {
        var secrets = new DesktopSecretStore(backend: null);
        var clipboard = Substitute.For<IClipboardService>();
        using var provider = AppServices.Create(
            new FixedAppPaths(),
            secrets,
            secrets,
            configureTestServices: services => services.AddSingleton(clipboard));

        Assert.Same(clipboard, provider.GetRequiredService<IClipboardService>());
    }

    [Fact]
    public async Task Create_ConfigureHttpClientDefaults_InterceptsOpenRouterAndTogetherAndFalNeedsNamedOverride()
    {
        var secrets = new DesktopSecretStore(backend: null);
        var network = new RecordingHandler();
        using var provider = AppServices.Create(
            new FixedAppPaths(),
            secrets,
            secrets,
            configureTestServices: services =>
            {
                services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => network));
                services.AddHttpClient("ImageGen.Internal.FalTransport").ConfigurePrimaryHttpMessageHandler(() => network);
            });
        var apiKeys = provider.GetRequiredService<ApiKeyService>();

        Assert.Equal(ApiKeyStatus.Valid, await apiKeys.SaveAsync(ProviderIds.OpenRouter, "test-openrouter", TestContext.Current.CancellationToken));
        Assert.Equal(ApiKeyStatus.Valid, await apiKeys.SaveAsync("fal", "test-fal", TestContext.Current.CancellationToken));
        Assert.Equal(ApiKeyStatus.Valid, await apiKeys.SaveAsync("together", "test-together", TestContext.Current.CancellationToken));

        Assert.Equal(["GET https://api.fal.ai/v1/models?limit=1", "GET https://api.together.ai/v1/models", "GET https://openrouter.ai/api/v1/key"],
            network.Requests.Select(request => $"{request.Method} {request.Uri}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Create_ConfigureHttpClientDefaults_DoesNotReplaceFalPrimaryHandler()
    {
        var secrets = new DesktopSecretStore(backend: null);
        var defaultHandler = new RecordingHandler();
        using var provider = AppServices.Create(
            new FixedAppPaths(),
            secrets,
            secrets,
            configureTestServices: services =>
                services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => defaultHandler)));
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("ImageGen.Internal.FalTransport");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:1", TestContext.Current.CancellationToken));

        Assert.Empty(defaultHandler.Requests);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!));
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            });
        }

        protected override void Dispose(bool disposing) { }
    }
}
