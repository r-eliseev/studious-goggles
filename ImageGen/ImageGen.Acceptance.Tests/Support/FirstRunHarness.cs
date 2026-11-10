using System.Net;
using Avalonia.Controls;
using Avalonia.Threading;
using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ImageGen.Acceptance.Tests.Support;

/// <summary>Minimal first-run composition; task 253 replaces it with the shared acceptance world.</summary>
public sealed class FirstRunHarness : IDisposable
{
    private readonly GenerationService _generations;

    public FirstRunHarness()
    {
        Paths = new TempAppPaths();
        Secrets = new DesktopSecretStore(backend: null);
        var openRouter = StubHttpMessageHandler.Status(HttpStatusCode.OK);
        var dialogs = Substitute.For<IDialogService>();
        Provider = AppServices.Create(Paths, Secrets, Secrets, openRouter, dialogs);
        Window = new MainWindow();
        Navigation = Provider.GetRequiredService<AvaloniaNavigationService>();
        Navigation.AttachHost(Window.PageArea);
        Window.ConfigureShortcuts(Provider.GetRequiredService<SessionsPageModel>(), Navigation);
        _generations = Provider.GetRequiredService<GenerationService>();
    }

    public TempAppPaths Paths { get; }

    public DesktopSecretStore Secrets { get; }

    public ServiceProvider Provider { get; }

    public MainWindow Window { get; }

    public AvaloniaNavigationService Navigation { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Navigation.GoToAsync(Routes.Sessions);
        await WaitUntilAsync(() => Window.PageArea.Content is ApiKeyView, cancellationToken);
    }

    public async Task<bool> IsApiKeyScreenShownAsync(CancellationToken cancellationToken)
    {
        await WaitUntilAsync(() => Window.PageArea.Content is ApiKeyView, cancellationToken);

        return Window.PageArea.Content is ApiKeyView && Navigation.CurrentRoute == Routes.ApiKeyFor("openrouter", firstRun: true);
    }

    public void Dispose()
    {
        var idle = _generations.WhenIdleAsync();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!idle.Wait(10))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Background acceptance work did not stop within 10 seconds.");
            }

            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
        Provider.Dispose();
        Paths.Dispose();
    }

    private async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The expected first-run page did not appear within 10 seconds.");
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, cancellationToken);
        }
    }
}
