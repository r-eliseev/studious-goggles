using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Providers;
using ImageGen.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class FirstRunSteps
{
    private readonly AppWorld _world;

    public FirstRunSteps(ScenarioContext scenario) => _world = scenario.Get<AppWorld>();

    [Then("the API key screen is shown instead of the session list")]
    public async Task ThenTheApiKeyScreenIsShownInsteadOfTheSessionList()
    {
        var isShown = await HeadlessApp.RunAsync(_ =>
        {
            return Task.FromResult(_world.Window.PageArea.Content is ApiKeyView
                && _world.Provider.GetRequiredService<AvaloniaNavigationService>().CurrentRoute == Routes.ApiKeyFor(ProviderIds.OpenRouter, firstRun: true));
        }, CancellationToken.None).ConfigureAwait(false);

        Assert.True(isShown, "The first-run API key screen was not shown instead of the session list.");
    }
}
