---
id: 277
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-12T20:13:36Z
created: 2026-11-11
---

# Avoid blocking disposal in the acceptance AppWorld restart test

## Описание
The complete acceptance test suite hangs in `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorldTests.cs`, in `RestartAsync_ReusesDataFolderAndSecretStore` (line 11). The test constructs `AppWorld` inside `HeadlessApp.RunAsync` and declares it with `using var`; after the callback returns, synchronous `AppWorld.Dispose()` calls `DisposeAsync().GetAwaiter().GetResult()` while still on the Avalonia UI dispatcher. Running this test alone with `dotnet ImageGen/ImageGen.Acceptance.Tests/bin/Debug/net10.0/ImageGen.Acceptance.Tests.dll -method '*AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore' -reporter verbose` starts the test and does not finish. The same hang occurs in the full acceptance suite. The task 257 scenarios pass independently (11/11); task 257's plan does not include `Support/AppWorldTests.cs`.

## Acceptance criteria
- `RestartAsync_ReusesDataFolderAndSecretStore` awaits `AppWorld.DisposeAsync` on every exit path instead of invoking synchronous `Dispose` on the Avalonia UI dispatcher.
- The isolated restart test completes and disposes its app world, including when an assertion fails.
- The full acceptance test suite completes successfully.

## Analysis
`AppWorld` implements both `IDisposable` and an asynchronous `DisposeAsync` method. Its synchronous `Dispose` blocks on `DisposeAsync().GetAwaiter().GetResult()`. `HeadlessApp.RunAsync` runs the test callback on the Avalonia UI dispatcher, so the test's `using var world` performs that blocking wait on the dispatcher when the callback exits. The existing `Steps/CommonSteps.cs` already awaits `DisposeAsync`, showing the intended cleanup pattern. No production code change is needed.

## Plan
1. Replace the synchronous `using var world` lifetime in the restart test with explicit `try/finally` cleanup that awaits `world.DisposeAsync()` after the assertions. This keeps cleanup on the same callback while avoiding the synchronous dispatcher wait and still cleans up if an assertion fails.
2. Run the restart test in isolation and confirm it completes; then run the complete acceptance suite to verify the regression is removed.
3. Run formatting for the acceptance test project and `bash scripts/check.sh`.

Files: `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorldTests.cs`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --filter FullyQualifiedName~AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

## Отчёт

## Review

## Комментарии

### 2026-11-11, codex-gpt6 — Proposed plan
Update `AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore` to await asynchronous AppWorld disposal instead of blocking the UI dispatcher. Add a regression assertion or test structure proving the test completes after restart and cleanup. Run the isolated test, the full acceptance suite, `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes`, and `bash scripts/check.sh`.

### 2026-11-12, codex-gpt6
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
