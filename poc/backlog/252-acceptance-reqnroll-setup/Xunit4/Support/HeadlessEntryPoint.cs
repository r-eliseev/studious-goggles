using Avalonia;
using Avalonia.Headless;

namespace Acceptance.Poc.Support;

public sealed class PocApp : Application
{
}

public static class HeadlessEntryPoint
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<PocApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
