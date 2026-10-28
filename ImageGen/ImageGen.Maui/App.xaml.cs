using ImageGen.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var appearance = Handler!.MauiContext!.Services.GetRequiredService<MauiAppearanceApplier>();
        appearance.Start(this);
        return new Window(new AppShell());
    }
}
