using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
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
        Assert.NotNull(provider.GetRequiredService<ApiKeyPageModel>());
    }
}
