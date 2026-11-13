---
id: 030
status: blocked
assignee:
claimed:
created: 2026-10-27
---

# Application identity and branding

## Description
Replace the template MAUI ApplicationId with the value already selected by the owner, `com.eliseev.imagegen`; set up the icon/splash screen and Windows publisher. Do not invent new identifiers or a publisher. The change is needed because the project still has `com.companyname.imagegen.maui`, and the manifest has `CN=User Name` and `User Name`.

Depends on: —
Environment: planning on Linux or Windows; editing the Windows manifest and checking Windows data/secret migration are Windows-only and must be a separate task before implementation starts.

The `blocked` status is due to real missing information from the owner, not waiting for task 016.

## Acceptance criteria
- ApplicationId matches the approved `com.eliseev.imagegen`; the owner provides the exact Publisher and PublisherDisplayName values.
- The owner provides or explicitly approves the icon/splash screen and the decision about migrating existing data/keys.
- Android/shared assets and the Windows-specific manifest/migration are split by environment into small, executable plans.
- If migration is selected, verify that the database, files and settings are preserved, that the app can be restarted, and that recovery works after a failure; do not copy secrets to a plaintext file.
- If there is no migration, the instructions clearly warn about a new profile and having to re-enter keys.

## Analysis
Verified: `MauiAppPaths` uses `FileSystem.Current.AppDataDirectory`, while Avalonia `DesktopAppPaths` uses a separate ImageGen/override path. Do not promise that changing the MAUI id moves or changes Avalonia data. Android also gets a new package id: the existing installation may remain as a separate app, with a different sandbox and key store. The behavior of the specific unpackaged Windows build must be checked on Windows: the csproj sets `WindowsPackageType=None`, so the actual data folder cannot be inferred from the package identity alone.

The known decision is already recorded: the ApplicationId was selected on 2026-10-28; do not ask for it again. The remaining questions are the exact Publisher/PublisherDisplayName, approved assets, whether to preserve existing MAUI Windows and Android data, and whether asking the user to re-enter keys is acceptable. Reading/copying the old protected store automatically requires design first.

## Plan
Prepare after the owner responds; do not write code before then:
1. Record the provided Publisher/PublisherDisplayName, links to approved SVG/assets, and the data/key migration policy for Windows and Android. If a value is not provided, keep the task blocked with a specific question.
2. Split the independent Linux/Windows part (ApplicationId, MauiIcon/MauiSplashScreen, shared instructions) from the Windows-only task (Package.appxmanifest, actual unpackaged app paths and migration). Assign separate task numbers; do not make a mixed patch here.
3. For the id change, describe Android update/side-by-side installation behavior, data paths before and after, when the old sandbox is available, and a supported export/import method if needed. Do not promise access to the old app's sandbox from the new id.
4. On Windows, determine the actual paths and SecureStorage availability in a test profile; only then plan an atomic, idempotent migration with backup and recovery. Do not delete the source data before successful verification.
5. Give each part an exact Files line, build and checks. Shared part: MAUI Android build; Windows part: Windows build and a manual identity change with test data. Finish with a report, up-to-date migration instructions, and `bash scripts/check.sh` as required by docs/tasks.md.

Files to analyze: `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, `ImageGen/ImageGen.Maui/Platforms/Windows/Package.appxmanifest`, `ImageGen/ImageGen.Maui/Resources/AppIcon/appicon.svg`, `ImageGen/ImageGen.Maui/Resources/AppIcon/appiconfg.svg`, `ImageGen/ImageGen.Maui/Resources/Splash/splash.svg`, `ImageGen/ImageGen.Maui/Services/MauiAppPaths.cs`, `ImageGen/ImageGen.Maui/Services/MauiSecretStore.cs`, `ImageGen/ImageGen.Avalonia/Platform/DesktopAppPaths.cs`, `docs/knowledge/dev-environment.md`.

## Report

### 2026-11-02 — partial branding implementation at the owner's direct request

The name Omnilite, the attached image reference, new vector MAUI icon/splash, and previously approved `com.eliseev.imagegen` were applied to all MAUI target frameworks. The Android id was not changed from releases after task 164; Windows/iOS/Mac Catalyst now use the shared id. Old data was neither deleted nor migrated. Verified: Presentation tests 466/466; MAUI Android build with 0 warnings/errors; Avalonia build with 0 warnings/errors; `dotnet format` for the two changed .NET projects and `scripts/check.sh` passed. Actual Windows data/secret paths were not checked.

The task remains blocked from full completion: exact `Publisher` and `PublisherDisplayName` values were not provided or invented; on Windows, the effect of the id change on unpackaged data/secret paths must be checked, and instructions for using the old profile/keys must be documented.

## Comments

### 2026-10-28, owner (chat responses, recorded by reviewer)
Values: `ApplicationId` = `com.eliseev.imagegen`. The owner is the publisher in `Package.appxmanifest` (confirm the exact name and `Publisher` string with the owner during planning). Changing `ApplicationId` changes the data folder and secret store on Windows; decide in the plan whether to migrate data. Prefer doing this before real data exists.

### 2026-10-29, claude-536f
Blocked: the owner must provide the `ApplicationId`, publisher, and icon (`docs/status.md`, "Open questions"). The task cannot be resolved without them. Changing `ApplicationId` changes the data folder on Windows, so a migration decision is needed. Keep the task without a plan until the blocker is removed; do not write code.

### 2026-10-29, codex-backlog-review
The real blocker is preserved: exact owner information and a separate Windows task are needed; do not wait for manual task 016. Historical comments are preserved.

### 2026-10-31, claude-audit
The Android `ApplicationId` part was completed in task 164 (conditional property for Android only, without data migration — owner's decision of 2026-10-31). The remaining parts here are Windows (`ApplicationId`, Publisher, data folder and secrets, migration), iOS/Mac Catalyst, icon, and splash screen.

### 2026-11-02, codex-gpt6
The owner provided an image reference and requested the Omnilite name and a change to all MAUI ApplicationIds. The shared branding work is complete; see the partial report above. Publisher and Windows data/secret paths remain open, so the task is not closed.
