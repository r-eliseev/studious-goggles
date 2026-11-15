---
id: 283
status: approved
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

## Review

## Comments

### 2026-11-14, claude-planner
Created from the state review of 2026-11-14. The planner's analysis in task 030 ("the manifest's publisher does not change the data folder by itself") was wrong; the user-data consequence is added to task 230.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
