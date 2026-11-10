using Reqnroll;

namespace ImageGen.Acceptance.Tests.Support;

[Binding]
public sealed class HeadlessHooks
{
    [BeforeTestRun]
    public static void StartSession() => HeadlessApp.Start();

    [AfterTestRun]
    public static Task StopSession() => HeadlessApp.StopAsync();
}
