using Avalonia.Headless;
using Reqnroll;

namespace Acceptance.Poc.Support;

[Binding]
public sealed class HeadlessHooks
{
    public static HeadlessUnitTestSession Session { get; private set; } = null!;

    [BeforeTestRun]
    public static void StartSession() => Session = HeadlessUnitTestSession.StartNew(typeof(HeadlessEntryPoint));

    [AfterTestRun]
    public static Task StopSession() => Task.Run(() => Session.Dispose());
}
