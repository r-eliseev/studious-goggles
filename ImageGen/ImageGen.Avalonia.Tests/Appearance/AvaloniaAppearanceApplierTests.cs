using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation.Appearance;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Appearance;

public class AvaloniaAppearanceApplierTests
{
    [AvaloniaFact]
    public void Apply_Gallery_ChangesAccentColor()
    {
        var app = (Application)Application.Current!;
        using var restore = Restore(app);
        var applier = new AvaloniaAppearanceApplier(Service(app), app);

        applier.Apply(new AppearanceSettings(AppearanceStyle.Gallery, AppearanceTheme.Light));

        Assert.True(app.TryFindResource("IgAccentColor", ThemeVariant.Light, out var accent));
        Assert.Equal(Color.Parse("#0F766E"), (Color)accent!);
    }

    [AvaloniaFact]
    public void Apply_Dark_SetsRequestedThemeVariant()
    {
        var app = (Application)Application.Current!;
        using var restore = Restore(app);
        var applier = new AvaloniaAppearanceApplier(Service(app), app);

        applier.Apply(new AppearanceSettings(AppearanceStyle.Quiet, AppearanceTheme.Dark));

        Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void Apply_System_SetsDefaultVariant()
    {
        var app = (Application)Application.Current!;
        using var restore = Restore(app);
        var applier = new AvaloniaAppearanceApplier(Service(app), app);

        applier.Apply(new AppearanceSettings(AppearanceStyle.Quiet, AppearanceTheme.System));

        Assert.Equal(ThemeVariant.Default, app.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void Apply_Notebook_ChangesMetaFontWithoutRestart()
    {
        var app = (Application)Application.Current!;
        using var restore = Restore(app);
        var applier = new AvaloniaAppearanceApplier(Service(app), app);

        applier.Apply(new AppearanceSettings(AppearanceStyle.Notebook, AppearanceTheme.Light));

        Assert.True(app.TryFindResource("IgFontMeta", out var font));
        Assert.Contains("IBM Plex Mono", ((FontFamily)font!).Name);
    }

    private static ImageGen.Presentation.Appearance.AppearanceService Service(Application app) =>
        ((ImageGen.Avalonia.App)app).Services!.GetRequiredService<ImageGen.Presentation.Appearance.AppearanceService>();

    private static IDisposable Restore(Application app) =>
        new Restorer(() => new AvaloniaAppearanceApplier(Service(app), app).Apply(AppearanceSettings.Default));

    private sealed class Restorer(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
