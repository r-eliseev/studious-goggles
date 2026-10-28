using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using ImageGen.Avalonia.Services;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Presentation;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Flows;

public class SettingsFlowTests
{
    [AvaloniaFact]
    public async Task OpenSettings_FromSessions_ShowsSettingsView()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        await shell.StartAsync();
        await shell.WaitUntilAsync(() => shell.Window.PageArea.Content is SessionsView, "список сессий");

        await shell.Provider.GetRequiredService<SessionsPageModel>().OpenSettingsCommand.ExecuteAsync(null);

        Assert.IsType<SettingsView>(shell.Window.PageArea.Content);
    }

    [AvaloniaFact]
    public async Task SelectGallery_ChangesAccentResource()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        var settings = shell.Provider.GetRequiredService<SettingsPageModel>();
        var appearance = shell.Provider.GetRequiredService<AppearanceService>();
        await settings.LoadCommand.ExecuteAsync(null);

        try
        {
            await settings.Styles.Single(s => s.Style == AppearanceStyle.Gallery).SelectCommand.ExecuteAsync(null);

            Assert.Equal(AppearanceStyle.Gallery, appearance.Current.Style);
            var app = (Application)Application.Current!;
            new AvaloniaAppearanceApplier(appearance, app).Start();
            Assert.True(app.TryFindResource("IgAccentColor", ThemeVariant.Light, out var accent));
            Assert.Equal(Color.Parse("#0F766E"), (Color)accent!);
        }
        finally
        {
            await appearance.SaveAsync(AppearanceSettings.Default, CancellationToken.None);
        }
    }
}
