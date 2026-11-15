---
id: 282
status: approved
assignee:
claimed:
created: 2026-11-14
---

# Acceptance tests on Windows: the log file stays locked when a world is deleted

## Description
Since task 261 the acceptance `AppWorld` writes the real file log (`Support/AppWorld.cs` line ~92, `AddImageGenFileLogging`). On Windows the scenario cleanup cannot delete the world's folder: PR #107 run [43436242955](https://github.com/r-eliseev/studious-goggles/actions/runs/43436242955), job `windows-tests`, three scenarios (`SettingsAndAppearanceFeature`, `FirstLaunchAndKeysFeature` twice) fail in `ScenarioCleanupAsync` with `IOException: The process cannot access the file '...\acceptance-world-<id>\logs\imagegen-20261113.log' because it is being used by another process`. On `main` this step is hidden: the Avalonia step before it fails first (task 281). The cause is unknown: `AddImageGenFileLogging` registers Serilog with `dispose: true`, and `AppWorld.StopCompositionAsync` disposes the provider before `Directory.Delete`.

Depends on: 281 (so that the acceptance step runs on Windows at all).
Environment: Linux for the code; the proof is the Windows `windows-tests` job (Windows CI).

## Acceptance criteria
- Step 1 findings in "Comments": with 281 merged, the failing scenarios on Windows (names, error), and who holds the handle when the folder is deleted. Check at least: (a) whether `provider.DisposeAsync()` in `StopCompositionAsync` actually disposes the `ILoggerFactory` and the Serilog logger (add a temporary Debug log or a unit test on Linux that the file handle is closed: on Linux, open the log file with `FileShare.None` after disposal); (b) whether a second world or the restart (`RestartAsync`) creates a second logger on the same file; (c) whether `StopCompositionAsync` times out (`WaitForTaskAsync`) and continues.
- Fix in the harness only (`Support/AppWorld.cs`): the log handle is closed before the folder is deleted, on every exit path, including restart. No change of product logging unless step 1 proves a product defect (then `blocked` with the evidence and a proposed product fix).
- `windows-tests` on the pull request: the acceptance step green.

## Plan
1. After 281 is merged: push a branch from `main`, read the Windows acceptance failure; the Linux experiment (a); record findings.
2. The minimal harness fix; a regression test in `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorldTests.cs` that after `DisposeAsync` (and after `RestartAsync` + `DisposeAsync`) the log file can be opened exclusively (fails before the fix if the handle leaks on Linux too; if it passes before the fix, keep it and rely on Windows CI as the proof).
3. Windows CI green.

Files: `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorld.cs`, `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorldTests.cs`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
# GitHub Actions: build.yml windows-tests green on the pull request
```

## Report

## Review

## Comments

### 2026-11-14, claude-planner
Created from the state review of 2026-11-14 (PR #107 was merged with this check red).
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
