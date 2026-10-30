using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Composition;
using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Avalonia.Tests.TestDoubles;

[assembly: AvaloniaTestApplication(typeof(ImageGen.Avalonia.Tests.TestAppBuilder))]

namespace ImageGen.Avalonia.Tests;

public static class TestAppBuilder
{
    /// <summary>
    /// The app of the tests uses a temporary data folder (deleted when the test process exits) and a key store in memory.
    /// The real data folder and the real key store are never opened by a test (task 048).
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var paths = new TempAppPaths();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                paths.Dispose();
            }
            catch (IOException)
            {
                // The log file may still be open; the temporary folder is left for the system to clean up.
            }
        };
        ImageGen.Avalonia.App.HostOverrides = new AppHostOverrides(paths, new DesktopSecretStore(backend: null));
        return AppBuilder.Configure<ImageGen.Avalonia.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
