---
id: 281
status: approved
assignee:
claimed:
created: 2026-11-14
---

# Red main: the background catalog refresh keeps imagegen.db open after the app is disposed (Windows)

## Description
`build.yml` → `windows-tests` is red on `main` since `1eae68e` (PR #109, task 235): runs [43487552864](https://github.com/r-eliseev/studious-goggles/actions/runs/43487552864) and [43593886799](https://github.com/r-eliseev/studious-goggles/actions/runs/43593886799) fail the same test, `ImageGen.Avalonia.Tests.Flows.SessionModelsFlowTests.LongCatalog_SaveAndCancelStayVisibleOnBothTabs`: `System.IO.IOException: The process cannot access the file 'imagegen.db' because it is being used by another process` at `TempAppPaths.Dispose()` ← `ShellHarness.Dispose()` (`ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs` line ~113). PR #107's run had two such failures. Linux does not lock open files, so the `avalonia` job is green.

Cause (from the code, to confirm by the fix): since 235, `AppInitializer.InitializeAsync` (`ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`) starts `ModelCatalogRefreshService.RefreshStaleAsync(CancellationToken.None)` fire-and-forget. Nothing cancels or awaits it, so when a test disposes the container and deletes the data folder, the refresh can still be writing the catalog (64 models in this test) to the database. The same task also outlives the app's own shutdown.

Depends on: —. Goes first: `main` is red, and this failure hides the Windows acceptance step that runs after it (task 282).
Environment: Linux for the code and tests; the proof is the `windows-tests` job on the pull request.

## Acceptance criteria
- `AppInitializer` owns a `CancellationTokenSource` for the refresh, passes its token to `RefreshStaleAsync`, keeps the refresh `Task`, and implements `IDisposable` and `IAsyncDisposable`: disposal cancels the token and waits for the refresh to end (at most 10 seconds; cancellation and errors are swallowed and logged at Debug). Disposal before initialization or twice is a no-op.
- `EnsureInitializedAsync` still does not wait for the refresh (the test of 235 stays green).
- `ModelCatalogRefreshService` stops between catalogs when the token is cancelled (check: it already passes the token to every call; add a test only if a gap is found).
- New tests in `AppInitializerTests`: disposal cancels the token given to the refresh; disposal waits until a refresh blocked on a gate finishes after cancellation.
- `windows-tests` on the pull request: `ImageGen.Avalonia.Tests` green. If it is still red with the same error in another test, `blocked` with the run link — do not add waits to tests.

## Analysis
- The container disposes singletons that implement `IDisposable`/`IAsyncDisposable`; `ShellHarness.Dispose` calls `Provider.Dispose()` before `Paths.Dispose()`, so no harness change is needed. The acceptance `AppWorld` disposes the provider asynchronously (`StopCompositionAsync`) — also covered.
- MAUI and Avalonia apps get the same clean shutdown for free.

## Plan
1. Failing tests in `ImageGen/ImageGen.Presentation.Tests/Services/AppInitializerTests.cs` (fail: `AppInitializer` is not disposable / the token is `None`).
2. The change in `AppInitializer`.
3. Push; check `windows-tests` (and that the Acceptance step after it now runs; its own failure, if any, is task 282 — record it in the Report).

Files: `ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`, `ImageGen/ImageGen.Presentation.Tests/Services/AppInitializerTests.cs`, `ImageGen/ImageGen.Core/Services/ModelCatalogRefreshService.cs` (only if the cancellation gap is found), `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogRefreshServiceTests.cs` (same condition).

### Verification
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
bash scripts/check.sh
# GitHub Actions: build.yml windows-tests green on the pull request
```

## Report

## Review

## Comments

### 2026-11-14, claude-planner
Created from the state review of 2026-11-14 (red `main`), at the owner's standing request to open fix tasks for problems found.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
