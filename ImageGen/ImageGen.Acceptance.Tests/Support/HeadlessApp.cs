using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace ImageGen.Acceptance.Tests.Support;

public sealed class HeadlessApp : Application
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);
    private static HeadlessUnitTestSession? _session;

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<HeadlessApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public static void Start()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp), AvaloniaTestIsolationLevel.PerAssembly);
        if (Interlocked.CompareExchange(ref _session, session, null) is not null)
        {
            session.Dispose();
            throw new InvalidOperationException("The headless Avalonia session has already been started.");
        }

        var isUiThread = session.Dispatch(() => Dispatcher.UIThread.CheckAccess(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (!isUiThread)
        {
            _ = Interlocked.Exchange(ref _session, null);
            Task.Run(session.Dispose).GetAwaiter().GetResult();
            throw new InvalidOperationException("The headless Avalonia dispatcher did not start on its UI thread.");
        }
    }

    public static async Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var session = Volatile.Read(ref _session)
            ?? throw new InvalidOperationException("The headless Avalonia session has not been started.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StepTimeout);

        return await session.Dispatch(() => action(timeout.Token), timeout.Token);
    }

    public static Task StopAsync()
    {
        var session = Interlocked.Exchange(ref _session, null);
        return session is null ? Task.CompletedTask : Task.Run(session.Dispose);
    }
}
