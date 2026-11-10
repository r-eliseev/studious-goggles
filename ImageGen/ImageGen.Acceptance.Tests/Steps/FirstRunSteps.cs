using ImageGen.Acceptance.Tests.Support;
using ImageGen.Core.Domain.Keys;
using Reqnroll;
using Xunit;

namespace ImageGen.Acceptance.Tests.Steps;

[Binding]
public sealed class FirstRunSteps
{
    private FirstRunHarness? _harness;

    [Given("the app has no saved OpenRouter key")]
    public async Task GivenTheAppHasNoSavedOpenRouterKey()
    {
        await HeadlessApp.RunAsync(async cancellationToken =>
        {
            _harness = new FirstRunHarness();
            var savedKey = await _harness.Secrets.GetAsync(SecretNames.OpenRouterApiKey, cancellationToken);
            Assert.Null(savedKey);
            return true;
        }, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    [When("the user starts the app")]
    public async Task WhenTheUserStartsTheApp()
    {
        var harness = _harness ?? throw new InvalidOperationException("The first-run harness has not been created.");
        await HeadlessApp.RunAsync(async cancellationToken =>
        {
            await harness.StartAsync(cancellationToken);
            return true;
        }, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    [Then("the API key screen is shown instead of the session list")]
    public async Task ThenTheApiKeyScreenIsShownInsteadOfTheSessionList()
    {
        var harness = _harness ?? throw new InvalidOperationException("The first-run harness has not been created.");
        var isShown = await HeadlessApp.RunAsync(
            cancellationToken => harness.IsApiKeyScreenShownAsync(cancellationToken),
            TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.True(isShown, "The first-run API key screen was not shown.");
    }

    [AfterScenario]
    public async Task DisposeHarnessAsync()
    {
        var harness = Interlocked.Exchange(ref _harness, null);
        if (harness is null)
        {
            return;
        }

        await HeadlessApp.RunAsync(_ =>
        {
            harness.Dispose();
            return Task.FromResult(true);
        }, CancellationToken.None).ConfigureAwait(false);
    }
}
