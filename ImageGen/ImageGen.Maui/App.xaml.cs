using ImageGen.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        // Android: the keyboard shrinks the window instead of panning it, so the pages (SafeAreaEdges="All") keep
        // their inputs above it (task 160). Other platforms ignore this setting.
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.UseWindowSoftInputModeAdjust(
            On<Microsoft.Maui.Controls.PlatformConfiguration.Android>(),
            Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var appearance = Handler!.MauiContext!.Services.GetRequiredService<MauiAppearanceApplier>();
        appearance.Start(this);
        return new Window(new AppShell());
    }
}
