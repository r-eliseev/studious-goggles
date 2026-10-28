using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Flows;

public class SessionModelsFlowTests
{
    [AvaloniaFact]
    public async Task SaveSelectionAndRuns_ReturnsToChatWithNewButtonText()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await shell.OpenNewChatAsync();

        await chat.OpenModelsCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionModelsView, "экран моделей");
        var models = (SessionModelsPageModel)((Control)shell.Window.PageArea.Content!).DataContext!;
        await shell.WaitUntilAsync(() => models.Models.Count == 2, "каталог загружен");

        models.Models.Single(model => model.Id == "vendor/beta").IsSelected = true;
        models.Models.Single(model => model.Id == SessionSettings.DefaultImageModelId).IsSelected = false;
        models.RunsPerModel = 3;
        await models.SaveCommand.ExecuteAsync(null);

        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView && chat.ModelsButtonText == string.Format(AppResources.ModelsButtonFormat, 1, 3), "подпись кнопки моделей");
        var saved = await shell.Provider.GetRequiredService<SessionService>().ListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([new ImageGen.Core.Domain.Models.ModelRef(ImageGen.Core.Providers.ProviderIds.OpenRouter, "vendor/beta")], saved.Single().Settings.ImageModels);
        Assert.Equal(3, saved.Single().Settings.RunsPerModel);
    }

    [AvaloniaFact]
    public async Task RunsCounter_ShowsModelValueAndWritesBackUserInput()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await shell.OpenNewChatAsync();
        await chat.OpenModelsCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionModelsView, "экран моделей");
        var models = (SessionModelsPageModel)((Control)shell.Window.PageArea.Content!).DataContext!;
        shell.Window.Show();
        shell.Window.UpdateLayout();
        var counter = ((Control)shell.Window.PageArea.Content!).GetVisualDescendants().OfType<NumericUpDown>().Single();

        models.RunsPerModel = 4;
        Assert.Equal(4m, counter.Value);

        counter.Value = 2m;
        Assert.Equal(2, models.RunsPerModel);
    }

    [AvaloniaFact]
    public async Task SearchFilter_KeepsSelectionOfHiddenModels()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await shell.OpenNewChatAsync();
        await chat.OpenModelsCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionModelsView, "экран моделей");
        var models = (SessionModelsPageModel)((Control)shell.Window.PageArea.Content!).DataContext!;
        await shell.WaitUntilAsync(() => models.Models.Count == 2, "каталог загружен");

        models.SearchText = "beta";
        Assert.Single(models.Models);

        models.SearchText = "";
        Assert.Equal(2, models.Models.Count);
        Assert.True(models.Models.Single(model => model.Id == SessionSettings.DefaultImageModelId).IsSelected);
    }

    private static async Task<ChatPageModel> OpenChatAsync(ShellHarness shell)
    {
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var session = await shell.Provider.GetRequiredService<SessionService>().CreateAsync(TestContext.Current.CancellationToken);
        await shell.Navigation.GoToAsync(Routes.ChatFor(session.Id));
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is ChatView, "чат");
        var chat = (ChatPageModel)((Control)shell.Window.PageArea.Content!).DataContext!;
        await shell.WaitUntilAsync(() => chat.ModelsButtonText.Length > 0, "лента загружена");
        return chat;
    }
}
