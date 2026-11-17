---
id: 283
status: done
assignee:
claimed:
created: 2026-11-14
---

# Windows UI E2E: the test writes providers.json to the old publisher folder

## Description
`ui-e2e.yml` → `test-windows` fails since task 030 changed the Windows publisher (PR #110; first failing run [43478771639](https://github.com/r-eliseev/studious-goggles/actions/runs/43478771639), on `main` [43593886862](https://github.com/r-eliseev/studious-goggles/actions/runs/43593886862)): `MockSessionTests.FirstRunWithMock_GeneratesImage` → `InvalidOperationException: Element 'E2E Mock' was not found on the desktop` (`MockSessionTests.cs` line 19). The other Windows E2E tests pass. The workflow stays green only because the job has `continue-on-error: true`.

Cause (verified 2026-11-14): the unpackaged MAUI app's data folder contains the publisher display name. The `preflight-windows-reports` artifact of run 43593886862 has `dataDir` = `C:\Users\runneradmin\AppData\Local\Roman Eliseev\com.eliseev.imagegen\Data`, while `ImageGen/ImageGen.E2E.Windows/WindowsTestBase.cs` lines 46–47 hard-code `"User Name", "com.eliseev.imagegen", "Data"`, so `providers.json` with the `e2e-mock` provider goes to a folder the app does not read.

Depends on: —.
Environment: Linux for the edit; the proof is the Windows `test-windows` job.

## Acceptance criteria
- `WindowsTestBase` does not hard-code the publisher: it reads `PublisherDisplayName` and the `Identity` `Name` from the `AppxManifest.xml` next to the tested `ImageGen.Maui.exe` (the build output has it; if not, from `ImageGen/ImageGen.Maui/Platforms/Windows/Package.appxmanifest` located from the repository root) and builds `<LocalApplicationData>\<PublisherDisplayName>\<Name>\Data`. A missing value fails the test setup with a clear message.
- `docs/knowledge/ui-e2e.md` (or `dev-environment.md`, "Logs"): one line — the unpackaged MAUI Windows data folder includes the publisher display name; the run that showed it.
- `test-windows` on the pull request: all Windows E2E tests pass; the recording of `FirstRunWithMock_GeneratesImage` ends on the generated image (check the artifact, ADR 0033).

## Plan
1. Check whether the build output (`windows-app` artifact or the workflow's build folder) contains `AppxManifest.xml`; choose the source accordingly.
2. The change in `WindowsTestBase`; the knowledge line.
3. Push, run `ui-e2e.yml`, check the artifact.

Files: `ImageGen/ImageGen.E2E.Windows/WindowsTestBase.cs`, `docs/knowledge/ui-e2e.md`, `docs/knowledge/dev-environment.md`.

### Verification
```
dotnet build ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj
dotnet format ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj --verify-no-changes
bash scripts/check.sh
# GitHub Actions: ui-e2e.yml test-windows green (job conclusion, not only the workflow)
```

## Report

The `windows-app` artifact from PR #105 run 43497273413 did not contain `AppxManifest.xml`, so `WindowsTestBase` now reads the manifest beside `APP_EXE` when available and otherwise loads `ImageGen/ImageGen.Maui/Platforms/Windows/Package.appxmanifest` from the repository root. It validates `Identity.Name` and `Properties.PublisherDisplayName` and uses both in the LocalApplicationData path. Added the confirmed publisher-specific folder to `docs/knowledge/ui-e2e.md`.

Verification:
- `dotnet build ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj` — passed, 0 warnings and 0 errors.
- `dotnet format ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj --verify-no-changes` — passed.
- `bash scripts/check.sh` — passed.
- Windows `ui-e2e.yml` `test-windows` for PR #112 — passed in run [43699409512](https://github.com/r-eliseev/studious-goggles/actions/runs/43699409512); all 5 tests passed. The TRX marks `FirstRunWithMock_GeneratesImage` as Passed. Inspected its 8.33-second, 1024×768 recording; the final frame shows the generated red-square image.
- The PR's separate `build.yml` `windows-tests` job still fails in the acceptance-test step after the Avalonia suite; task 282 tracks that file-lock issue.

No difference from the approved plan. A manual Windows app run is unavailable in this Linux environment; the Windows CI workflow supplied the planned verification.

## Review
Reviewed 2026-11-17 by claude-planner. `WindowsTestBase` reads `PublisherDisplayName` and `Identity Name` from the manifest (beside the exe, else the repository's `Package.appxmanifest`); the knowledge line is added. Windows `test-windows` passed all 5 tests in run [43699409512](https://github.com/r-eliseev/studious-goggles/actions/runs/43699409512); the Report states the recording ends on the generated image.
Verdict: done.

## Comments

### 2026-11-14, claude-planner
Created from the state review of 2026-11-14. The planner's analysis in task 030 ("the manifest's publisher does not change the data folder by itself") was wrong; the user-data consequence is added to task 230.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
