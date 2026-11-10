using ImageGen.Acceptance.Tests.Support;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Providers;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
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

    [When("the user enters API key {string}")]
    public Task WhenTheUserEntersApiKey(string key) => HeadlessApp.RunAsync(_ =>
    {
        ApiKeyModel.ApiKey = key;
        return Task.FromResult(true);
    }, TestContext.Current.CancellationToken);

    [When("the user checks the API key")]
    public Task WhenTheUserChecksTheApiKey() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        var model = ApiKeyModel;
        await model.SaveCommand.ExecuteAsync(null);
        await _world.WaitUntilAsync(() => !model.IsBusy, "the API key check to finish", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the API key status shows it was rejected")]
    public void ThenTheApiKeyStatusShowsItWasRejected() => Assert.Equal(
        string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.ProviderKeyInvalidFormat, "OpenRouter"),
        ApiKeyModel.StatusText);

    [Then("the API key status shows a connection error")]
    public void ThenTheApiKeyStatusShowsAConnectionError() => Assert.Equal(
        string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.ProviderKeyNetworkErrorFormat, "OpenRouter"),
        ApiKeyModel.StatusText);

    [Then("the API key is accepted and the session list opens")]
    public async Task ThenTheApiKeyIsAcceptedAndTheSessionListOpens()
    {
        await _world.WaitUntilAsync(
            () => _world.Provider.GetRequiredService<AvaloniaNavigationService>().CurrentRoute == Routes.Sessions,
            "the session list route", TestContext.Current.CancellationToken);
        Assert.Equal("e2e-mock-key", await _world.Secrets.GetAsync(
            ImageGen.Core.Domain.Keys.SecretNames.OpenRouterApiKey, TestContext.Current.CancellationToken));
    }

    [Then("the API key screen stays closed after restart")]
    public async Task ThenTheApiKeyScreenStaysClosedAfterRestart()
    {
        await HeadlessApp.RunAsync(async cancellationToken =>
        {
            await _world.RestartAsync(cancellationToken);
            return true;
        }, TestContext.Current.CancellationToken);
        Assert.IsNotType<ApiKeyView>(_world.Window.PageArea.Content);
        Assert.Equal(Routes.Sessions, _world.Provider.GetRequiredService<AvaloniaNavigationService>().CurrentRoute);
    }

    [Then("the entered API key is not saved")]
    public async Task ThenTheEnteredApiKeyIsNotSaved()
    {
        var saved = await _world.Secrets.GetAsync(ImageGen.Core.Domain.Keys.SecretNames.OpenRouterApiKey, TestContext.Current.CancellationToken);
        Assert.Null(saved);
        Assert.DoesNotContain(_world.Network.Requests, request => request.Address.Contains("sk-or-acceptance-invalid", StringComparison.Ordinal)
            || request.Body?.Contains("sk-or-acceptance-invalid", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("sk-or-acceptance-invalid", ApiKeyModel.StatusText, StringComparison.Ordinal);
        var logsDirectory = Path.Combine(_world.DataDirectory, "logs");
        if (Directory.Exists(logsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(logsDirectory))
            {
                Assert.DoesNotContain("sk-or-acceptance-invalid", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken), StringComparison.Ordinal);
            }
        }
    }

    [When("the user opens settings")]
    public Task WhenTheUserOpensSettings() => HeadlessApp.RunAsync(async cancellationToken =>
    {
        await _world.Provider.GetRequiredService<AvaloniaNavigationService>().GoToAsync(Routes.Settings);
        var model = SettingsModel;
        await _world.WaitUntilAsync(() => model.ProviderKeys.Count == 3, "provider keys to load in Settings", cancellationToken);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("settings show the OpenRouter key ending in {string}")]
    public void ThenSettingsShowTheOpenRouterKeyEndingIn(string suffix) =>
        Assert.Equal(string.Format(System.Globalization.CultureInfo.CurrentCulture, AppResources.SettingsKeyConnectedFormat, suffix),
            Assert.Single(SettingsModel.ProviderKeys, key => key.ProviderId == ProviderIds.OpenRouter).KeyText);

    [Then("settings show {string} as not connected")]
    public void ThenSettingsShowProviderAsNotConnected(string providerId) =>
        Assert.Equal(AppResources.SettingsKeyMissing,
            Assert.Single(SettingsModel.ProviderKeys, key => key.ProviderId == providerId).KeyText);

    [When("the user changes the OpenRouter key")]
    public Task WhenTheUserChangesTheOpenRouterKey() => HeadlessApp.RunAsync(async _ =>
    {
        await Assert.Single(SettingsModel.ProviderKeys, key => key.ProviderId == ProviderIds.OpenRouter).ChangeCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    [Then("the previously saved OpenRouter key still works")]
    public async Task ThenThePreviouslySavedOpenRouterKeyStillWorks() =>
        Assert.Equal("e2e-mock-key", await _world.Secrets.GetAsync(SecretNames.OpenRouterApiKey, TestContext.Current.CancellationToken));

    [When("the user removes the OpenRouter key")]
    public Task WhenTheUserRemovesTheOpenRouterKey() => HeadlessApp.RunAsync(async _ =>
    {
        await Assert.Single(SettingsModel.ProviderKeys, key => key.ProviderId == ProviderIds.OpenRouter).RemoveCommand.ExecuteAsync(null);
        return true;
    }, TestContext.Current.CancellationToken);

    private SettingsPageModel SettingsModel => Assert.IsType<SettingsPageModel>(Assert.IsType<SettingsView>(_world.Window.PageArea.Content).DataContext);

    private ApiKeyPageModel ApiKeyModel => Assert.IsType<ApiKeyPageModel>(Assert.IsType<ApiKeyView>(_world.Window.PageArea.Content).DataContext);
}
