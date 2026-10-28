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
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ImageGen.Avalonia.Tests.TestDoubles;

/// <summary>
/// The app as the real composition builds it, with the outside world replaced: OpenRouter answers from a stub, the
/// data folder is temporary, secrets stay in memory and dialogs are substitutes. Not started until StartAsync.
/// </summary>
public sealed class ShellHarness : IDisposable
{
    /// <param name="fileLogging">Writes the log to the data folder at Debug, as the app does in a debug build.</param>
    public ShellHarness(
        StubHttpMessageHandler? openRouter = null,
        IDialogService? dialogs = null,
        bool fileLogging = false)
    {
        Paths = new TempAppPaths();
        Secrets = new DesktopSecretStore(backend: null);
        OpenRouter = openRouter ?? StubHttpMessageHandler.Status(HttpStatusCode.OK);
        Dialogs = dialogs ?? Substitute.For<IDialogService>();
        var logDirectory = ImageGenLogging.LogDirectoryFor(Paths.DataDirectory);
        Provider = AppServices.Create(
            Paths,
            Secrets,
            Secrets,
            OpenRouter,
            Dialogs,
            fileLogging ? builder => builder.AddImageGenFileLogging(logDirectory, LogLevel.Debug) : null);
        Window = new MainWindow();
        Navigation = Provider.GetRequiredService<AvaloniaNavigationService>();
        Navigation.AttachHost(Window.PageArea);
    }

    public TempAppPaths Paths { get; }

    public DesktopSecretStore Secrets { get; }

    public StubHttpMessageHandler OpenRouter { get; }

    public IDialogService Dialogs { get; }

    public ServiceProvider Provider { get; }

    public MainWindow Window { get; }

    public AvaloniaNavigationService Navigation { get; }

    /// <summary>Opens the root page the same way App does at startup.</summary>
    public Task StartAsync() => Navigation.GoToAsync(Routes.Sessions);

    /// <summary>Creates the database, as the sessions page does on its first appearance.</summary>
    public Task InitializeDatabaseAsync() =>
        Provider.GetRequiredService<ImageGen.Presentation.Services.AppInitializer>().EnsureInitializedAsync();

    public Task SaveKeyAsync(string key) => Secrets.SetAsync(SecretNames.OpenRouterApiKey, key, CancellationToken.None);

    /// <summary>Runs the UI loop and waits until the condition holds (background work resumes on the UI thread).</summary>
    public async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Условие не выполнилось: " + description);
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    /// <summary>Creates a session with the default settings and opens its chat; waits until the timeline is loaded.</summary>
    public async Task<ChatPageModel> OpenNewChatAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SaveKeyAsync("sk-or-test");
        await InitializeDatabaseAsync();
        var session = await Provider.GetRequiredService<SessionService>().CreateAsync(cancellationToken);
        await Navigation.GoToAsync(Routes.ChatFor(session.Id));
        await WaitUntilAsync(() => Window.PageArea.Content is ChatView, "чат");
        var chat = (ChatPageModel)((Control)Window.PageArea.Content!).DataContext!;
        await WaitUntilAsync(() => chat.ModelsButtonText.Length > 0, "лента загружена");
        return chat;
    }

    public void Dispose()
    {
        Provider.Dispose();
        Paths.Dispose();
    }
}
