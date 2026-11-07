using Avalonia;
using Avalonia.Android;

namespace LibVlcAndroidPoc;

[global::Android.App.Activity(Label = "LibVLC Avalonia PoC", MainLauncher = true, Exported = true)]
public sealed class MainActivity : AvaloniaMainActivity
{
}

[global::Android.App.Application]
public sealed class MainApplication(nint javaReference, Android.Runtime.JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
}

public sealed class App : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());

    public override void OnFrameworkInitializationCompleted() => base.OnFrameworkInitializationCompleted();
}
