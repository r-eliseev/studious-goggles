using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia;

[Activity(Label = "ImageGen Probe", Theme = "@style/MyTheme.NoActionBar", Icon = "@drawable/icon", MainLauncher = true, Exported = true, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Intent?.GetBooleanExtra("probe-secret-write", false) is true)
        {
            MobileApp.RunSecretDiagnosticAsync(writeMarker: true).FireAndForgetSafeAsync();
        }
        else if (Intent?.GetBooleanExtra("probe-secret-read", false) is true)
        {
            MobileApp.RunSecretDiagnosticAsync(writeMarker: false).FireAndForgetSafeAsync();
        }
    }
}
