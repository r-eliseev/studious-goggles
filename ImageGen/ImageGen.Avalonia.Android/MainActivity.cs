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
    public static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        var cacheDirectory = global::Android.App.Application.Context.CacheDir?.AbsolutePath;
        if (!string.IsNullOrWhiteSpace(cacheDirectory))
        {
            System.Environment.SetEnvironmentVariable("TMPDIR", cacheDirectory);
        }

        base.OnCreate(savedInstanceState);
    }

    protected override void OnResume()
    {
        base.OnResume();
        Current = this;
    }

    protected override void OnPause()
    {
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }

        base.OnPause();
    }
}
