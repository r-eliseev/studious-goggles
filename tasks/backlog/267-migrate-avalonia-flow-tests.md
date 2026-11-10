---
id: 267
status: planned
assignee:
claimed:
created: 2026-11-09
---

# Migrate the Avalonia flow tests to acceptance scenarios

## Description
The owner's decision of 2026-11-09: the acceptance scenarios replace the current end-to-end style tests (ADR 0031, item 7). `ImageGen.Avalonia.Tests/Flows/*FlowTests.cs` (35 tests in 10 files on `ShellHarness`) check user flows in C# code over the same headless composition the scenarios use. After the scenarios of tasks 254–265 exist, every flow test is mapped to a catalog scenario; a covered flow test is deleted, an uncovered one becomes a new catalog row and a scenario first.

Not in scope: `Views/`, `Services/`, `Appearance/`, `Composition/` and other tests of `ImageGen.Avalonia.Tests` — they are unit tests of controls and services, not user flows, and stay.

Depends on: 254–265 (all done in `main`).
Environment: Linux or Windows (headless).

## Acceptance criteria
- A mapping table in "Analysis" (filled in step 1): every test method of `Flows/` → a catalog id (existing or new) and a verdict "covered" / "new scenario" / "keep" (only with a reason, for example a check of the control tree that a scenario cannot express cheaply).
- For "new scenario": a row in `docs/specs/acceptance-scenarios.md` (the next free id of the area) and a green scenario in the area's feature file before the flow test is deleted.
- A flow test is deleted only when its scenario is green and checks at least the same assertions (compare them assertion by assertion and list the differences in the "Report").
- After the migration `Flows/` contains only tests with a "keep" verdict; if it is empty, the folder is deleted. `ShellHarness` and `OpenRouterFakes` are deleted if nothing uses them any more.
- The test counts before and after are in the "Report"; both test projects are green.

## Analysis
Files of `ImageGen/ImageGen.Avalonia.Tests/Flows/` and their test counts (2026-11-09): `StartFlowTests` 8, `SessionModelsFlowTests` 10, `ChatFlowTests` 6, `ChatKeyboardFlowTests` 3, `SettingsFlowTests` 3, `AssistantRetryFlowTests` 1, `EmptyChatExampleFlowTests` 1, `LoggingFlowTests` 1, `OtherModelFlowTests` 1, `SessionParametersFlowTests` 1. Expected coverage areas: KEY (start), MOD (session models, parameters), CHAT/GEN/ACT (chat, retry, other model), SET (settings), SES (empty chat example). `ChatKeyboardFlowTests` and `LoggingFlowTests` probably need new scenarios (keyboard shortcuts of `docs/specs/chat-sessions.md`, "Keyboard"; the log file content).

## Plan
1. Step 1: read every flow test, fill the mapping table, set the verdicts. Commit the table alone.
2. For each "new scenario": the catalog row, the scenario (failing first, then the bindings), green.
3. Delete the covered flow tests one file at a time; after each file run both test projects.
4. Delete unused doubles; update the catalog statuses.

Files: `ImageGen/ImageGen.Avalonia.Tests/Flows/` (deleted files), `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs`, `OpenRouterFakes.cs` (deleted if unused), `ImageGen/ImageGen.Acceptance.Tests/Features/`, `Steps/`, `docs/specs/acceptance-scenarios.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Execution order (for Luna)
- First read only the files of the "Files" line and the named tests; check signatures against the current `main`.
- Steps strictly in order (ub-mode): a failing test → run it, make sure it fails exactly for the described reason → minimal code → the test is green.
- Do not change files outside the "Files" line, do not rename members beyond the plan, do not fix neighboring findings — they go to separate tasks.
- If the code differs from the plan, do not improvise: stop and write a question in "Comments".
- Run `dotnet` commands one at a time, not in parallel. After each changed project — `dotnet format <project> --verify-no-changes`.
- Write everything in English. In the "Report": what changed, commands and results, differences from the plan.
- Never delete a flow test whose assertions are not all covered by a green scenario.

## Report

## Comments

### 2026-11-09, claude-bdd-plan
Created after the owner's decision that the acceptance scenarios replace the current end-to-end tests. The plan waits for approval.
