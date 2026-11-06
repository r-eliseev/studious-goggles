using Android.Content.PM;
using Android.OS;
using Avalonia.Android;

namespace ImageGen.Avalonia;

[Activity(
    Label = "Omnilite Avalonia",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
