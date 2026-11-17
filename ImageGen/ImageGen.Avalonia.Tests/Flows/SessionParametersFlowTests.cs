using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Flows;

public class SessionParametersFlowTests
{
    [AvaloniaFact]
    public async Task ChatOpensParameters_SaveStoresValueAndReturnsToChat()
    {
        var ct = TestContext.Current.CancellationToken;
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var sessions = shell.Provider.GetRequiredService<SessionService>();
        var session = await sessions.CreateAsync(ct);
        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "chat");

        await shell.Navigation.GoToAsync(Routes.SessionParametersFor(session.Id));
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionParametersView, "parameters screen");
        var model = (SessionParametersPageModel)((Control)shell.Window.PageArea.Content!).DataContext!;
        await shell.WaitUntilAsync(() => model.OutputFormatOptions.Count > 0, "list loaded");
        model.SelectedOutputFormat = model.OutputFormatOptions.Single(o => o.Value == "webp");

        await model.SaveCommand.ExecuteAsync(null);

        var stored = await sessions.GetAsync(session.Id, ct);
        Assert.Equal("webp", stored!.Settings.Parameters.OutputFormat);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "back to chat");
    }
}
