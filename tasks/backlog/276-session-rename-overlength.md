---
id: 276
status: approved
assignee:
claimed:
created: 2026-11-11
---

# Reject overlength session rename titles

## Description

The accepted sessions specification says that renaming to more than 40 characters is not accepted (`docs/specs/acceptance-scenarios.md`, SES-04). The current behavior silently truncates the title to 40 characters: `ImageGen/ImageGen.Core/Services/SessionService.cs`, `RenameAsync` trims the input and takes `trimmed[..Session.MaxTitleLength]`. The UI then updates the row to that truncated title in `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `RenameSessionAsync`.

Reproduction: in acceptance scenario `ImageGen/ImageGen.Acceptance.Tests/Features/Sessions.feature`, queue the rename prompt result `This title is deliberately longer than forty characters` and rename a new session. Expected: the title remains `New session`; actual: it becomes `This title is deliberately longer than f` (40 characters). The empty-title case is rejected and displays an alert.

Depends on: 255.
Environment: Linux or Windows.

## Acceptance criteria
- Owner decision of 2026-11-13: a title longer than `Session.MaxTitleLength` (40) after trimming is rejected, not truncated.
- Core: `SessionService.RenameAsync` throws `ArgumentException` when the trimmed title is longer than 40 characters; the session is not updated and no change is notified. Exactly 40 characters is accepted. The automatic title from the first message (`SetTitleFromFirstMessageAsync`) still truncates to 40 (S1.2, SES-02 — unchanged).
- Presentation: `SessionsPageModel.RenameSessionAsync` checks the trimmed length before calling the service and shows `dialogs.AlertAsync(AppResources.ErrorTitle, AppResources.RenameTooLong)`; the row keeps its title. New resource `RenameTooLong` = "A session name can have at most 40 characters." in `AppResources.resx` and its accessor in `AppResources.cs` (as `RenameEmpty`).
- Web: `PATCH /api/sessions/{id}` with a 41-character title answers 400 `invalid_request` (already mapped from `ArgumentException`); a test proves it.
- Acceptance: the SES-04 long-title scenario has no `@ignore @bug-276`, adds `And the app shows a rename error`, and is green; catalog row SES-04 is `done`.
- `docs/specs/chat-sessions.md` S1.2: "(up to 40 characters; an empty or longer title is not accepted)".

## Analysis
- `ImageGen/ImageGen.Core/Services/SessionService.cs` `RenameAsync` (line ~261) truncates; `SetTitleFromFirstMessageAsync` (line ~274) truncates and stays so.
- `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs` `RenameAsync_Long_TruncatesToMaxLength` (line ~610) locks in the old behavior: it is replaced by `RenameAsync_LongerThanMax_ThrowsAndKeepsTitle` and `RenameAsync_ExactlyMax_IsAccepted`.
- `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs` `RenameSessionAsync` (line ~495) already rejects an empty title with `RenameEmpty`; follow that pattern. Tests next to `Rename_SavesNewTitleAndShowsIt` in `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`.
- Web endpoint: `ImageGen/ImageGen.Web/Api/WebApiEndpoints.cs` `MapPatch` catches `ArgumentException` → 400; no code change expected there. Test in `ImageGen/ImageGen.Web.Tests/SessionsApiTests.cs`.
- The MAUI prompt has `maxLength: 4000` (`ImageGen.Maui/Services/ShellDialogService.cs`); not changed here.
- No overlap with in-progress tasks; 255 (approved) also edits `Sessions.feature` — whoever goes second rebases; only the SES-04 long-title scenario is touched here.

## Plan
1. Core: replace the truncation test with the two new tests; run — the "throws" test fails because the title is truncated; change `RenameAsync`; green.
2. Presentation: test "a 41-character title shows `RenameTooLong` and keeps the title, the service is not called"; fails (no resource / no check); add the resource and the check; green.
3. Web: test for 400 on a 41-character title; it should pass after step 1 — if it already passes before step 1 is merged, note it in the Report.
4. Acceptance: remove `@ignore @bug-276`, add the error step; green. Catalog and spec lines.

Files: `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`, `ImageGen/ImageGen.Web.Tests/SessionsApiTests.cs`, `ImageGen/ImageGen.Acceptance.Tests/Features/Sessions.feature`, `docs/specs/acceptance-scenarios.md`, `docs/specs/chat-sessions.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Web.Tests/ImageGen.Web.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
bash scripts/check.sh
```

### Execution order
- Steps strictly in order (ub-mode); run `dotnet` commands one at a time; do not change files outside "Files"; on a mismatch with the code, stop and ask in "Comments".

## Report

## Review

## Comments

### 2026-11-11, codex-gpt6
Proposed plan:
- Reject rename input longer than `Session.MaxTitleLength` instead of silently truncating it; keep the existing title unchanged and show the same kind of user-facing validation as the empty-title case.
- Add a `SessionService` regression test for a 41-character title and verify valid renames remain unchanged.
- Once task 255 is merged, remove `@ignore @bug-276` from the SES-04 long-title scenario and update the SES-04 catalog status to `done`.
- Candidate files: `ImageGen/ImageGen.Core/Services/SessionService.cs`, its Core test, `ImageGen/ImageGen.Acceptance.Tests/Features/Sessions.feature`, and `docs/specs/acceptance-scenarios.md`.

### 2026-11-13, owner (in the chat, recorded by claude-planner)
Asked whether a rename longer than 40 characters is rejected or truncated (the code and its unit test truncate; the catalog SES-04 says "not accepted"; S1.2 is ambiguous): "Reject + alert". The plan above follows it.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
