using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Composition;

public class AppServicesTests
{
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
