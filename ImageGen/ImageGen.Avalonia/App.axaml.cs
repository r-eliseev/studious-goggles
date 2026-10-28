using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Platform;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia;

public partial class App : Application
{
    /// <summary>Composition root of the running app, set when the framework is initialized.</summary>
    public ServiceProvider? Services { get; private set; }

    /// <summary>The main window, created at startup whether or not the platform has a desktop lifetime.</summary>
    public MainWindow? MainWindow { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var paths = new DesktopAppPaths();
        var secrets = DesktopSecretStore.ForCurrentPlatform(paths);
        Services = AppServices.Create(paths, secrets, secrets);
        MainWindow = new MainWindow();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = MainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
