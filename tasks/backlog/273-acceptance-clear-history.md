---
id: 273
status: review
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

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/Sessions.feature`, `ImageGen/ImageGen.Acceptance.Tests/Steps/SessionsSteps.cs`, `ImageGen/ImageGen.Acceptance.Tests/Steps/CommonSteps.cs`, `docs/specs/acceptance-scenarios.md`, `Support/FakeOpenRouter.cs`, `Support/FakePlatform.cs` and `Support/AppWorld.cs` (additive test-harness changes only, amendment of 2026-11-13).

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

### 2026-11-19, codex-20261117
- Added SES-11 coverage for declining confirmation and clearing history from an open chat. The passing clear scenario verifies two finished assistant-triggered runs, removal of session image files, return to the empty session list, and preservation of the OpenRouter key and Gallery/dark appearance.
- Added a separate ignored SES-11 spending scenario tagged `@bug-288`. Its two generation cards and image files are present and each card shows `$0.014`, but the Spending screen shows `$0` instead of `$0.028` before history is cleared. This means ledger retention cannot yet be tested. Created `tasks/backlog/288-spending-assistant-costs-across-sessions.md` with evidence and a proposed diagnostic plan; it remains `todo` for planner analysis. Updated the SES-11 catalog status to `blocked (288: spending total is already $0 before history clear in the two-session assistant-run setup)`.
- TDD: the SES-11 scenarios first failed with missing bindings; after adding the planned acceptance steps, the focused run passed 2 scenarios and skipped the one tracked by 288.
- Verification: focused SES-11 — 2 passed, 1 skipped; full acceptance — 100 passed, 3 skipped on retry. Two earlier full-suite runs exposed known intermittent failures already recorded by task 286 (CardActions failed-generation menu, then the monthly-limit restart value); isolated `SPD-05` passed before the full suite passed. `dotnet format ... --verify-no-changes`, `bash scripts/check.sh`, `git diff --check`, the locale regression, task-role regression and release-notes regression all passed.
- Difference from plan: the spending preservation criterion is blocked by the pre-clear spending discrepancy and is tracked in task 288; no production behavior was changed.

## Comments
### 2026-11-10, claude-bdd-plan
Created at the owner's request to turn the scenarios unblocked by finished tasks into work. Approved by the owner's instruction "approve all tasks with plans" (2026-11-10).

### 2026-11-13, claude-planner — plan amendment
Preventive amendment for all open acceptance tasks: tasks 255, 256 and 261 were blocked only because a needed fake response or world helper was outside their `Files:` line. Additive changes in `Support/FakeOpenRouter.cs`, `Support/FakePlatform.cs` and `Support/AppWorld.cs` are now allowed (new configurable responses, catalog fields, world helpers; no change of behavior other features rely on; no product code). Product/specification discrepancies are still `@ignore @bug-NNN` plus a new task. If two open tasks need the same harness change, the second one reuses it from `main`.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
