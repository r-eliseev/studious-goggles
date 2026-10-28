using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Platform;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Composition;

public class AppServicesTests
{
    [Fact]
    public void Create_ValidateOnBuild_ResolvesPageModels()
    {
        using var provider = AppServices.Create(new FixedAppPaths(), new InMemorySecretStore());

        Assert.NotNull(provider.GetRequiredService<SessionsPageModel>());
        Assert.NotNull(provider.GetRequiredService<ChatPageModel>());
        Assert.NotNull(provider.GetRequiredService<ApiKeyPageModel>());
    }
}
