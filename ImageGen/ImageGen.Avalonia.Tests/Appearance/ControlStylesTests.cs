using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation.Appearance;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Appearance;

public class ControlStylesTests
{
    [AvaloniaFact]
    public void PrimaryButton_UsesAccentBackground()
    {
        var app = (Application)Application.Current!;
        using var restore = Restore(app);
        new AvaloniaAppearanceApplier(Service(app), app).Apply(new AppearanceSettings(AppearanceStyle.Quiet, AppearanceTheme.Light));
        var button = new Button { Classes = { "primary" } };
        var window = Show(button);

        Assert.Equal(Color.Parse("#2563C9"), ((ISolidColorBrush)button.Background!).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void PrimaryButton_AfterStyleChange_UsesNewAccent()
    {
        var app = (Application)Application.Current!;
        using var restore = Restore(app);
        var applier = new AvaloniaAppearanceApplier(Service(app), app);
        applier.Apply(new AppearanceSettings(AppearanceStyle.Quiet, AppearanceTheme.Light));
        var button = new Button { Classes = { "primary" } };
        var window = Show(button);

        applier.Apply(new AppearanceSettings(AppearanceStyle.Gallery, AppearanceTheme.Light));

        Assert.Equal(Color.Parse("#0F766E"), ((ISolidColorBrush)button.Background!).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void ErrorTextBox_UsesErrBorder()
    {
        var app = (Application)Application.Current!;
        using var restore = Restore(app);
        new AvaloniaAppearanceApplier(Service(app), app).Apply(new AppearanceSettings(AppearanceStyle.Quiet, AppearanceTheme.Light));
        var box = new TextBox { Classes = { "error" } };
        var window = Show(box);

        Assert.Equal(Color.Parse("#B42318"), ((ISolidColorBrush)box.BorderBrush!).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void StatusFiltered_UsesWarnSoftBackground()
    {
        var app = (Application)Application.Current!;
        using var restore = Restore(app);
        new AvaloniaAppearanceApplier(Service(app), app).Apply(new AppearanceSettings(AppearanceStyle.Quiet, AppearanceTheme.Light));
        var badge = new Border { Classes = { "status", "filtered" } };
        var window = Show(badge);

        Assert.Equal(Color.Parse("#FBF1DC"), ((ISolidColorBrush)badge.Background!).Color);
        window.Close();
    }

    [AvaloniaFact]
    public void IconTextBlock_UsesLucideFont()
    {
        var icon = new TextBlock { Classes = { "icon" }, Text = "" };
        var window = Show(icon);

        Assert.Contains("lucide", icon.FontFamily.Name, StringComparison.OrdinalIgnoreCase);
        window.Close();
    }

    private static Window Show(Control control)
    {
        var window = new Window { Content = control };
        window.Show();
        return window;
    }

    private static ImageGen.Presentation.Appearance.AppearanceService Service(Application app) =>
        ((App)app).Services!.GetRequiredService<ImageGen.Presentation.Appearance.AppearanceService>();

    private static IDisposable Restore(Application app) =>
        new Restorer(() => new AvaloniaAppearanceApplier(Service(app), app).Apply(AppearanceSettings.Default));

    private sealed class Restorer(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
