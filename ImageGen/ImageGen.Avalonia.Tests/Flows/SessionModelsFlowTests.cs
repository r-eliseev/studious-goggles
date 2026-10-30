using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
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

    [AvaloniaFact]
    public async Task Tabs_TwoTabsWithImageModelsFirst()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await shell.OpenNewChatAsync();
        await chat.OpenModelsCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionModelsView, "экран моделей");
        shell.Window.Show();
        shell.Window.UpdateLayout();

        var view = (SessionModelsView)shell.Window.PageArea.Content!;
        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
        var items = tabs.Items.Cast<TabItem>().ToArray();

        Assert.Equal(2, items.Length);
        Assert.Equal(AppResources.ImageModelsTab, items[0].Header);
        Assert.Equal(AppResources.AssistantModelTitle, items[1].Header);
        Assert.Equal(0, tabs.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task Tabs_SwitchingTabItem_ChangesPageModelTab()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await shell.OpenNewChatAsync();
        await chat.OpenModelsCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionModelsView, "экран моделей");
        shell.Window.Show();
        shell.Window.UpdateLayout();

        var view = (SessionModelsView)shell.Window.PageArea.Content!;
        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
        var model = (SessionModelsPageModel)view.DataContext!;
        tabs.SelectedIndex = 1;
        Assert.Equal(SessionModelsTab.AssistantModel, model.SelectedTab);

        model.ShowImageModelsCommand.Execute(null);

        Assert.Equal(0, tabs.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task Tabs_EachTabHasOwnScrollViewer_ButtonsOutsideTabs()
    {
        using var shell = new ShellHarness(openRouter: OpenRouterFakes.Create());
        var chat = await shell.OpenNewChatAsync();
        await chat.OpenModelsCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionModelsView, "экран моделей");
        shell.Window.Show();
        shell.Window.UpdateLayout();

        var view = (SessionModelsView)shell.Window.PageArea.Content!;
        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
        var save = view.FindControl<Button>("SaveButton")!;
        var cancel = view.FindControl<Button>("CancelButton")!;
        Assert.Empty(save.GetVisualAncestors().OfType<TabControl>());
        Assert.Empty(cancel.GetVisualAncestors().OfType<TabControl>());

        for (var index = 0; index < 2; index++)
        {
            tabs.SelectedIndex = index;
            shell.Window.UpdateLayout();
            var item = Assert.IsType<TabItem>(tabs.Items[index]);
            Assert.NotEmpty(item.GetLogicalDescendants().OfType<ScrollViewer>());
        }
    }

    [AvaloniaFact]
    public async Task LongCatalog_SaveAndCancelStayVisibleOnBothTabs()
    {
        using var shell = new ShellHarness(openRouter: LongCatalogHandler());
        shell.Window.Width = 600;
        shell.Window.Height = 640;
        shell.Window.Show();
        var chat = await shell.OpenNewChatAsync();
        await chat.OpenModelsCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(
            () => shell.Window.PageArea.Content is SessionModelsView view
                && ((SessionModelsPageModel)view.DataContext!).Models.Count == 60,
            "длинный каталог моделей");
        shell.Window.UpdateLayout();

        var view = (SessionModelsView)shell.Window.PageArea.Content!;
        var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
        var save = view.FindControl<Button>("SaveButton")!;
        var cancel = view.FindControl<Button>("CancelButton")!;
        Assert.Empty(save.GetVisualAncestors().OfType<ScrollViewer>());
        Assert.Empty(cancel.GetVisualAncestors().OfType<ScrollViewer>());

        for (var index = 0; index < 2; index++)
        {
            tabs.SelectedIndex = index;
            shell.Window.UpdateLayout();
            Assert.True(save.IsVisible);
            Assert.True(cancel.IsVisible);
            AssertInsideView(save, view);
            AssertInsideView(cancel, view);
        }
    }

    private static void AssertInsideView(Control control, Control view)
    {
        var topLeft = global::Avalonia.VisualExtensions.TranslatePoint(control, default, view);
        var bottomRight = global::Avalonia.VisualExtensions.TranslatePoint(
            control, new global::Avalonia.Point(control.Bounds.Width, control.Bounds.Height), view);
        Assert.NotNull(topLeft);
        Assert.NotNull(bottomRight);
        Assert.True(topLeft.Value.X >= 0 && topLeft.Value.Y >= 0);
        Assert.True(bottomRight.Value.X <= view.Bounds.Width && bottomRight.Value.Y <= view.Bounds.Height);
    }

    private static StubHttpMessageHandler LongCatalogHandler()
    {
        var images = Enumerable.Range(0, 60).Select(index => new
        {
            id = index == 0 ? SessionSettings.DefaultImageModelId : $"vendor/image-{index:00}",
            name = index == 0 ? "Default image model" : $"Long catalog image model {index:00}",
            supported_parameters = new { seed = true },
        });
        var imagesJson = JsonSerializer.Serialize(new { data = images });
        var chatsJson = """{"data":[{"id":"vendor/assistant","name":"Assistant model","supported_parameters":["tools"],"architecture":{"input_modalities":["text"]},"context_length":1000}]}""";

        return new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path.EndsWith("/images/models", StringComparison.Ordinal) ? imagesJson
                : path.EndsWith("/models", StringComparison.Ordinal) ? chatsJson
                : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        });
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
