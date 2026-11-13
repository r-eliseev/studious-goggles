---
id: 030
status: review
assignee:
claimed:
created: 2026-10-27
---

# Application identity and branding

## Description
Replace the template MAUI ApplicationId with the value already selected by the owner, `com.eliseev.imagegen`; set up the icon/splash screen and Windows publisher. Do not invent new identifiers or a publisher. The change is needed because the project still has `com.companyname.imagegen.maui`, and the manifest has `CN=User Name` and `User Name`.

Depends on: —
Environment: Linux (the edit) and GitHub Actions (the Windows build); the Windows data/secret check is task 230.



## Acceptance criteria
- `ImageGen/ImageGen.Maui/Platforms/Windows/Package.appxmanifest`: `Identity` `Publisher="CN=Roman Eliseev"` and `<PublisherDisplayName>Roman Eliseev</PublisherDisplayName>` (owner's values of 2026-11-13). `Identity Name`, `Version`, `PhoneIdentity` and every other line unchanged.
- The MAUI Windows target builds (the `maui` job of `build.yml` on the pull request is green).
- The Windows data and key folders after the id change are not part of this task: task 230 measures and migrates them; task 016 holds the manual Windows check. The Android, icon and splash parts were finished earlier (see the partial report).
- After this change nothing of 030 remains open: the Report lists what was done in 164, on 2026-11-02 and here, and the task goes to `review`.

## Analysis
- The project is unpackaged (`<WindowsPackageType>None</WindowsPackageType>` in `ImageGen.Maui.csproj`), so the manifest's publisher does not sign anything and does not change the data folder by itself; it is the identity shown by Windows.
- Task 230 (approved) covers the data and `SecureStorage` path after the `ApplicationId` change; no overlap with the manifest.

## Plan
1. Edit the two values in `Package.appxmanifest`.
2. Push the branch; the `maui` job of `build.yml` builds the Windows target. Record the run link in the Report.
3. Report: a summary of all parts of 030 (164 — Android id; 2026-11-02 — name Omnilite, assets, MAUI ids; this change — publisher) and that the data part is 230.

Files: `ImageGen/ImageGen.Maui/Platforms/Windows/Package.appxmanifest`.

### Verification
```
grep -n 'Publisher' ImageGen/ImageGen.Maui/Platforms/Windows/Package.appxmanifest
bash scripts/check.sh
# GitHub Actions: build.yml, job maui — green
```

## Report

Completed task 030 in three stages: task 164 changed the Android `ApplicationId`; on 2026-11-02 the owner-requested Omnilite name, icon/splash assets, and shared MAUI application ids were applied; this change sets the Windows publisher metadata to the owner's values (`CN=Roman Eliseev` / `Roman Eliseev`). The Windows data and SecureStorage path after the id change remains task 230. The Windows manifest itself was not otherwise changed.

Verification: `grep -n 'Publisher' ImageGen/ImageGen.Maui/Platforms/Windows/Package.appxmanifest` and `bash scripts/check.sh` passed. The required Windows MAUI `maui` job passed in [GitHub Actions run 43476442529](https://github.com/r-eliseev/studious-goggles/actions/runs/43476442529/job/132061373788).

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

### 2026-11-13, owner (in the chat, recorded by claude-planner)
Windows publisher: `Publisher="CN=Roman Eliseev"`, `PublisherDisplayName` "Roman Eliseev". The planner reduced the task to the manifest edit; the data and key path after the id change is task 230.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
