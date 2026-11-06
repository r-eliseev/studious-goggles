using Avalonia;
using Avalonia.Android;

namespace ImageGen.Avalonia;

[Application]
public sealed class MainApplication(nint javaReference, global::Android.Runtime.JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<MobileApp>(javaReference, transfer)
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder);
}
