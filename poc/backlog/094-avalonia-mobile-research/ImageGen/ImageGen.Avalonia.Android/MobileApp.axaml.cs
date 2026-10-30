using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Platform;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation.Appearance;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia;

public sealed class MobileApp : global::Avalonia.Application
{
    private const string DiagnosticSecretName = "probe-secret-check";
    private const string DiagnosticSecretValue = "avalonia-mobile-probe-marker-v1";

    public static ServiceProvider Services { get; private set; } = null!;
    public static AndroidSecretStore Secrets { get; private set; } = null!;
    public static bool Started { get; set; }
    public static Views.MainView? CurrentView { get => Views.MainView.CurrentView; set => Views.MainView.CurrentView = value; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var paths = new AndroidAppPaths();
        global::Android.Util.Log.Info("ImageGenProbe", $"DataDirectory={paths.DataDirectory}; LocalApplicationData={Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
        Secrets = new AndroidSecretStore(paths.DataDirectory);
        Services = MobileAppServices.Create(paths, Secrets, Secrets);
        new AvaloniaAppearanceApplier(Services.GetRequiredService<AppearanceService>(), this).Start();
        if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = () => new Views.MainView();
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static async Task RunSecretDiagnosticAsync(bool writeMarker)
    {
        try
        {
            if (writeMarker)
            {
                await Secrets.SetAsync(DiagnosticSecretName, DiagnosticSecretValue, CancellationToken.None);
            }

            var persisted = !writeMarker && await Secrets.GetAsync(DiagnosticSecretName, CancellationToken.None) == DiagnosticSecretValue;
            global::Android.Util.Log.Info("ImageGenProbe", $"persisted={persisted.ToString().ToLowerInvariant()}; file-exists={Secrets.HasStoredSecret(DiagnosticSecretName).ToString().ToLowerInvariant()}");
        }
        catch
        {
            global::Android.Util.Log.Info("ImageGenProbe", "persisted=false");
        }
    }
}
