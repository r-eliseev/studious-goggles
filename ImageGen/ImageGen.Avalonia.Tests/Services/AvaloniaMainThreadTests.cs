using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ImageGen.Avalonia.Services;

namespace ImageGen.Avalonia.Tests.Services;

public class AvaloniaMainThreadTests
{
    [AvaloniaFact]
    public void BeginInvokeOnMainThread_RunsActionOnUiThread()
    {
        var mainThread = new AvaloniaMainThread();
        var onUiThread = false;

        mainThread.BeginInvokeOnMainThread(() => onUiThread = Dispatcher.UIThread.CheckAccess());
        Dispatcher.UIThread.RunJobs();

        Assert.True(onUiThread);
    }
}
