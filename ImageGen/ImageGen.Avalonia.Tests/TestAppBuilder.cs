using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(ImageGen.Avalonia.Tests.TestAppBuilder))]

namespace ImageGen.Avalonia.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<ImageGen.Avalonia.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
