using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
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
}
