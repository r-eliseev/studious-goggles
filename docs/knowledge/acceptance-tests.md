# Acceptance tests

## Reqnroll with Avalonia Headless

Checked on 2026-11-10 with .NET SDK 10.0.403, Reqnroll.xunit.v3 3.3.4 and Avalonia.Headless 12.1.4. A minimal Reqnroll scenario that starts `HeadlessUnitTestSession` in a `[BeforeTestRun]` hook and checks `Dispatcher.UIThread.CheckAccess()` from a dispatched step passes with both `xunit.v3` 4.0.2 and 3.2.2. The prototype is in `poc/backlog/252-acceptance-reqnroll-setup/`.

The scenario continuation may execute on the Avalonia dispatcher thread after awaiting `HeadlessUnitTestSession.Dispatch`. Disposing the session synchronously from `[AfterTestRun]` on that thread waits for the dispatcher thread itself and hangs. Dispose it from a worker thread, for example by returning `Task.Run(() => session.Dispose())` from the hook.

Package references:

- [Reqnroll.xunit.v3 3.3.4](https://www.nuget.org/packages/Reqnroll.xunit.v3/3.3.4)
- [xunit.v3 4.0.2](https://www.nuget.org/packages/xunit.v3/4.0.2)
- [Avalonia.Headless 12.1.4](https://www.nuget.org/packages/Avalonia.Headless/12.1.4)

Reqnroll 3.3 writes generated `*.feature.cs` files beside the feature files by default. Set `ReqnrollUseIntermediateOutputPathForCodeBehind` to `true` to keep generated files in the intermediate `obj/` directory ([Reqnroll build setup](https://docs.reqnroll.net/latest/installation/setup-project.html)).
