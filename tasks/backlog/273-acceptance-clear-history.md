---
id: 273
status: approved
assignee:
claimed:
created: 2026-11-10
---

# Acceptance scenario: clearing the whole history (SES-11)

## Description
The catalog row SES-11 waited for task 229 (full history clearing), which is done. Task 255 (the Sessions feature) is in progress and its plan does not include SES-11, so the scenario is a separate task that goes after 255.

Behavior (`docs/specs/chat-sessions.md`, S1.4 — the history-clearing item): Settings offers deleting the whole history with confirmation; sessions, related records and media files are deleted; an open chat returns to the empty session list; provider keys, settings and the spending ledger are kept.

Depends on: 255 (the shared session steps and `Features/Sessions.feature`).
Environment: Linux or Windows (headless).

## Acceptance criteria
- SES-11 in `Features/Sessions.feature` with the tags `@SES-11 @S1.4`; green or `@ignore @bug-NNN` with a task.
- Given two sessions with a finished run each (real runs through the assistant fake, not DB writes), the user clears the history in Settings (`SettingsPageModel.ClearHistoryAsync`) and confirms in the dialog fake → the session list is empty, the `images/<sessionId>/` folders are gone, the OpenRouter key is still saved (the Settings key mask), the appearance settings are unchanged, the spending screen still shows the earlier total.
- Declining the confirmation → nothing is deleted.
- The catalog row SES-11 status is updated.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs` (`ClearHistoryAsync`), task 229's "Report" (in `tasks/DONE.md` after archiving: `git show` of the path) for the exact behavior and texts.
- Steps for sessions, runs, Settings and spending exist after 254, 255, 261 and 262; reuse them, add only missing ones to `Steps/CommonSteps.cs`.

## Plan
1. The declining case first (shorter), then the full case; each: the scenario fails for the described reason → bindings → green.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/Sessions.feature`, `ImageGen/ImageGen.Acceptance.Tests/Steps/SessionsSteps.cs`, `ImageGen/ImageGen.Acceptance.Tests/Steps/CommonSteps.cs`, `docs/specs/acceptance-scenarios.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Execution order (for Luna)
- The expectation comes from the specification, not the code; on a discrepancy — `@ignore @bug-NNN`, a new task, do not fix the code here.
- Steps act like the user (page model commands, dialogs); do not call repositories except in `Given` steps that prepare the world and in "the file exists" checks.
- Run `dotnet` commands one at a time. Write everything in English; in the "Report": scenarios, commands and results.

## Report

## Comments
### 2026-11-10, claude-bdd-plan
Created at the owner's request to turn the scenarios unblocked by finished tasks into work. Approved by the owner's instruction "approve all tasks with plans" (2026-11-10).
