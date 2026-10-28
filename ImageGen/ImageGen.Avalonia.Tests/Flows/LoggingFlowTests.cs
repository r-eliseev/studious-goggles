using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.Logging;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Flows;

public class LoggingFlowTests
{
    private const string SecretKey = "sk-or-SECRET-DO-NOT-LOG-9999";

    [AvaloniaFact]
    public async Task ChatWithAssistantAndGeneration_LogFileShowsTheChainAndNoKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create(), fileLogging: true);
        await shell.SaveKeyAsync(SecretKey);
        await shell.InitializeDatabaseAsync();
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(cancellationToken);
        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "чат");
        var chat = (ChatPageModel)((Control)shell.Window.PageArea.Content!).DataContext!;
        await shell.WaitUntilAsync(() => chat.ModelsButtonText.Length > 0, "лента загружена");

        chat.InputText = "draw a red fox";
        await chat.SendCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => chat.Items.OfType<RunItemViewModel>().Any(), "прогон в ленте");
        await shell.Provider.GetRequiredService<GenerationService>().WhenIdleAsync();

        // Disposing the container closes the file, so that everything written so far is on disk.
        shell.Provider.Dispose();
        var logText = ReadLogs(ImageGenLogging.LogDirectoryFor(shell.Paths.DataDirectory));

        Assert.DoesNotContain(SecretKey, logText, StringComparison.Ordinal);
        Assert.Contains("Navigation to", logText, StringComparison.Ordinal);
        Assert.Contains("/chat/completions", logText, StringComparison.Ordinal);
        Assert.Contains("Assistant turn in session", logText, StringComparison.Ordinal);
        Assert.Contains("is Succeeded", logText, StringComparison.Ordinal);
    }

    private static string ReadLogs(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return string.Empty;
        }

        return string.Join("\n", Directory.GetFiles(directory, "imagegen-*.log").Select(File.ReadAllText));
    }
}
