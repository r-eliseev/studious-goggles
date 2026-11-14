---
id: 230
status: approved
assignee:
claimed:
created: 2026-11-07
---

# MAUI Windows: data and keys after changing ApplicationId

## Description
Review finding from 2026-11-04 (commit `4b9e0b3`, task 030): the MAUI Windows target changed `ApplicationId` from `com.companyname.imagegen.maui` to `com.eliseev.imagegen` without migrating data. A previous comment in `ImageGen.Maui.csproj` warned that on Windows the id determines the data and secret folders. For an unpackaged app, `FileSystem.Current.AppDataDirectory` (data folder, `MauiAppPaths`) and `SecureStorage` (keys, `MauiSecretStore`) appear to depend on the id; after an update, sessions, images and keys may seem to "disappear" (the old folder remains on disk). Actual Windows paths have not been measured (`docs/status.md`, task 030).

Assumption (the owner may choose otherwise and restore the old id before migration): data is migrated automatically on the first launch of the new version.

Depends on: —
Environment: **Windows only** (path measurement, build and manual check); migration logic is shared and tested on Linux.

## Acceptance criteria
- Measure and record in `docs/knowledge/dev-environment.md` ("Logs"/"Packages"): the MAUI Windows data folder for the old id (release ≤ v0.1.10; first log line `ImageGen … Data: …`) and the new id; where unpackaged-app `SecureStorage` stores values (data-folder file, registry, etc.) and whether it depends on the id.
- `LegacyDataMigration.TryMigrate(string legacyDirectory, string currentDirectory, ILogger)` in `ImageGen.Presentation` (no platform types): if `currentDirectory` has no `imagegen.db` (database filename is `SqliteDatabase.FileName`) and `legacyDirectory` does, copy all contents of `legacyDirectory` into a temporary folder beside `currentDirectory`, then rename it to `currentDirectory` (if `currentDirectory` exists and is empty except for `logs/`, merge the contents); do not delete the old folder; write one Information log (file count, source and destination, no contents). Any error is a Warning; the app starts with empty data (as it does now), and retries on the next launch.
- Call it in `MauiProgram` under `#if WINDOWS` **before** creating the logger and opening the database; compute the old folder path using the measured rule (step 1), not a path copied from another machine.
- Keys: if `SecureStorage` is portable (for example, a file in the data folder), copy it with the data. Otherwise, after data migration, the first launch opens the key screen (as it already does when there is no key) and shows the message "Provider keys could not be moved after the update; enter them again." once (resource).
- Tests (Linux, temporary folders): migrate when the new folder is empty; do nothing when the new folder has a database; do nothing when the old folder has no database; a copy error leaves the new folder without partial data; logs in the new folder do not prevent migration.
- Manual check on Windows in task 016: install a release with the old id, create a session and key, install the new version — the session remains; the key remains or the message is shown.

## Analysis
`ImageGen/ImageGen.Maui/ImageGen.Maui.csproj` (`ApplicationId`), `ImageGen/ImageGen.Maui/Services/MauiAppPaths.cs` (`FileSystem.Current.AppDataDirectory`), `ImageGen/ImageGen.Maui/Services/MauiSecretStore.cs` (`SecureStorage.Default`), `ImageGen/ImageGen.Maui/MauiProgram.cs` (around line 62: `paths`, `logDirectory`, logging), `ImageGen/ImageGen.Data/SqliteDatabase.cs` (database filename), task 030 (`tasks/backlog/030-app-identity.md`, comments). The measurement step cannot be done on Linux: if the executor has no Windows machine, do only the shared part and tests, and leave exact measurement steps for the owner in "Comments".

## Plan
1. On Windows, measure the paths and `SecureStorage` location (see "Acceptance criteria") and record them.
2. Write failing `LegacyDataMigrationTests` (Presentation.Tests).
3. Implement `LegacyDataMigration` and the message resource.
4. Call it from `MauiProgram` under `#if WINDOWS`, computing the old path; build the Windows target (CI).
5. Update item 016 and the knowledge file; add a comment to 030 noting that data migration was moved here.

Files: `ImageGen/ImageGen.Presentation/Services/LegacyDataMigration.cs` (new), `ImageGen/ImageGen.Presentation.Tests/Services/LegacyDataMigrationTests.cs` (new), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `docs/knowledge/dev-environment.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/030-app-identity.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```

### Execution order (for Luna)
- First read only the files in the "Files" line and the named tests; compare signatures with current `main`.
- Follow the steps in order (ub-mode): failing test → run it and confirm it fails for the described reason → minimal fix → green test.
- Do not change files outside the "Files" line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature, test already exists), do not improvise; stop and write a question in "Comments".
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the "Report", record what changed, commands and results, and differences from the plan.

## Report

## Comments

### 2026-11-07, claude-audit
Created from the 2026-11-04 review finding (030, Windows ApplicationId changed without migration). Assumption: migrate the data rather than revert the id. Plan for Luna.

### 2026-11-07, owner (in chat, recorded by claude-audit)
Instruction: "create tasks for problems that do not have tasks"; the plan was approved under the earlier instruction "approve all tasks that come up".

### 2026-11-14, claude-planner — plan amendment
New fact (task 283): the unpackaged MAUI Windows data folder contains the publisher display name. Run 43593886862 (`preflight-windows-reports`, `dataDir`) shows `%LOCALAPPDATA%\Roman Eliseev\com.eliseev.imagegen\Data` after task 030 changed the publisher from "User Name". So Windows users of releases before 030 have their data in a third place, `%LOCALAPPDATA%\User Name\com.eliseev.imagegen\Data`. The legacy-folder rule of this task now covers an ordered list of candidates: first `User Name\com.eliseev.imagegen`, then the pre-id-change folder measured in step 1. `LegacyDataMigration.TryMigrate` keeps its contract and is called with the first candidate that has `imagegen.db`. Add a test with two candidates where only the second has a database. The manual check in 016 installs a release from before 030.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
