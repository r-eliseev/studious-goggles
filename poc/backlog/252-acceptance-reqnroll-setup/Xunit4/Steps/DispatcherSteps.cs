using Avalonia.Threading;
using Reqnroll;
using Xunit;

namespace Acceptance.Poc.Steps;

[Binding]
public sealed class DispatcherSteps
{
    [Then("the step runs on the Avalonia UI thread")]
    public async Task ThenTheStepRunsOnTheAvaloniaUiThread()
    {
        var result = await Support.HeadlessHooks.Session.Dispatch(
            () => $"{Environment.CurrentManagedThreadId}/{Dispatcher.UIThread.CheckAccess()}",
            TestContext.Current.CancellationToken);

        Assert.True(result.EndsWith("/True", StringComparison.Ordinal), result);
    }
}
