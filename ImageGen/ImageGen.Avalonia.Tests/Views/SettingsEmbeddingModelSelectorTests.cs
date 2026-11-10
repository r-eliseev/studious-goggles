using System.Net;
using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Views;

public sealed class SettingsEmbeddingModelSelectorTests
{
    [AvaloniaFact]
    public async Task SettingsView_EmbeddingModelSelector_IsAvailableAndNamed()
    {
        using var shell = new ShellHarness(new StubHttpMessageHandler(request =>
        {
            var json = request.RequestUri!.AbsolutePath.EndsWith("/embeddings/models", StringComparison.Ordinal)
                ? """{"data":[{"id":"vendor/model","name":"Embedding model","description":"test","context_length":4096,"pricing":{"prompt":"0.00000001"}}]}"""
                : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }));
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();

        await shell.Navigation.GoToAsync(Routes.Settings);
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SettingsView, "settings view");
        var settings = shell.Provider.GetRequiredService<SettingsPageModel>();
        await settings.LoadCommand.ExecuteAsync(null);
        await shell.WaitUntilAsync(() => settings.EmbeddingModels.Count > 0, "embedding model catalog");
        shell.Window.UpdateLayout();

        var view = Assert.IsType<SettingsView>(shell.Window.PageArea.Content);
        var selector = view.FindControl<ComboBox>("SemanticEmbeddingModelPicker");
        Assert.NotNull(selector);
        Assert.Equal("Search embedding models", AutomationProperties.GetName(selector));
    }
}
