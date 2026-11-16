---
id: 255
status: review
assignee:
claimed:
created: 2026-11-09
---

# Acceptance scenarios: session list, title, deletion, and default settings

## Description
Automate the “Sessions” section of the catalog `docs/specs/acceptance-scenarios.md`: SES-01 … SES-10. SES-11 (clear all history) follows task 229 and is out of scope here. File: `Features/Sessions.feature`, tag: `@sessions`.

Depends on: 253.
Environment: Linux or Windows (headless).

## Acceptance criteria
- SES-01 … SES-10 are in `.feature`; each passes or is tagged `@ignore @bug-NNN` with a task.
- SES-02: a 60-character message → the title contains exactly the first 40 characters (compare strings; do not trim whitespace unless required by the specification — check S1.2 and the code; if unclear, ask in “Comments”).
- SES-04: renaming to an empty string or 41 characters does not change the title; record what the user sees (an error or a disabled button) in “Report”, according to the code.
- SES-05: declining the confirmation dialog (the `IDialogService` fake answers “no”) leaves the session; accepting deletes the session, including after `Restart`.
- SES-09: “Today” and “Earlier” use `FakeTimeProvider`; a session created “yesterday” (advance time with the “Given current time <date time>” step) appears under “Earlier”. The time step belongs in `CommonSteps`.
- SES-07: changing the default assistant model in Settings does not change the model in an existing session; a new session receives the new default.
- Update the SES-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs` (`Sessions`, `Today`, `Earlier`, `NewSessionAsync`, `StartExampleAsync`, `Examples`), `SessionItemViewModel.cs` (find the rename and delete commands), and `ChatPageModel.cs` (`Title`).
- Core: `ImageGen/ImageGen.Core/Services/SessionService.cs`; title rules are there or in `HistoryService.cs`.
- Defaults for new sessions: `SettingsPageModel.SaveDefaultAssistantModelAsync`, `defaults.json` (`docs/specs/chat-sessions.md`, S1.3).
- Flow tests: `Flows/EmptyChatExampleFlowTests.cs` (SES-10), `Views/SessionsViewTests.cs`.

## Plan
Implement one scenario at a time, SES-01 → SES-10, following the TDD cycle in “Execution order”. Get the run count for SES-08 from actual runs through the assistant (“Given the assistant responds with a `generate_images` call …”), not by writing to the database.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/Sessions.feature`, new `Steps/SessionsSteps.cs`, `Steps/CommonSteps.cs`, `docs/specs/acceptance-scenarios.md`, `Support/FakePlatform.cs` (a queued `PromptAsync` result, approved 2026-11-10), `Support/FakeOpenRouter.cs`, and `Support/AppWorld.cs` (additive test-harness changes only: configurable fake responses, catalog fields, and world helpers; no change to behavior used by other features and no product code).

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=sessions"
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
(The tag filter applies if Reqnroll passes tags as xUnit categories; task 252 records the behavior in `docs/knowledge/acceptance-tests.md`.)

### Execution order (for Luna)
- First read only the files in the “Files” line, the named tests, and the relevant sections of `docs/specs/acceptance-scenarios.md`; compare signatures with the current `main`.
- Follow the steps in order (ub-mode): add a scenario to `.feature`, run it and confirm it fails because a step is unbound or an expectation is unmet, then add the smallest binding and make the scenario pass. One scenario per commit (include the scenario id in the message).
- Derive scenario expectations from the specification, not the code. If the code behaves differently, do not adapt the scenario or fix the code: use `@ignore @bug-NNN`, create a task with reproduction steps using `scripts/new-task.sh --publish backlog <name>`, and mark the catalog “discrepancy (NNN)”.
- Steps act as the user: call page-model commands, select items, and navigate. Do not call repositories, Core services, or SQL directly, except in “Given” steps that prepare the world and checks that verify a file exists.
- Put a new shared step in `Steps/CommonSteps.cs`; first check whether an equivalent step already exists with different wording.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong in separate tasks.
- If the code differs from the plan (missing method or different signature), do not improvise: stop and record a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In “Report”, list passing scenarios, ignored scenarios and their tasks, commands and results, and deviations from the plan. Update the scenario statuses in `docs/specs/acceptance-scenarios.md`.

## Report

- Implemented: SES-01, SES-02, SES-03, SES-06 and SES-09. SES-04 empty-title case passes; its overlength case is `@ignore @bug-276` because the current code truncates instead of rejecting.
- SES-05 confirmation decline, confirmed deletion, and absence after restart pass through the session row command.
- Acceptance harness cleanup advances its `FakeTimeProvider` before stopping app services, so delayed semantic-index refreshes do not leave teardown waiting on frozen test time. AppWorld teardown is asynchronous.
- Not implemented: SES-07, SES-08 and SES-10. SES-07 is blocked by the approved plan's file list: the fake chat model catalog is hard-coded empty, so Settings has no second assistant model to select. The required `Support/FakeOpenRouter.cs` change is not authorized by the current plan; see the latest comment.
- Verification so far: category runs passed for SES-02, SES-03 and SES-05; SES-04 had one pass and one expected skip. The full acceptance suite has not completed successfully after these additions. Formatting and `scripts/check.sh` remain to be run after the plan mismatch is resolved.
- Final work on 2026-11-13: SES-07, SES-08 and SES-10 are implemented. SES-07 configures two fake OpenRouter chat models, changes the default in Settings, and proves an existing session retains its assistant model while a new session uses the new default. SES-08 sends two user messages that each receive a `generate_images` tool response, then checks the session row shows `· 2 runs`. SES-10 chooses the first empty-list example through `StartExampleCommand` and verifies its exact text is sent once in the new chat and sets its title. The fake chat-model catalog remains unchanged by default for other scenarios.
- Updated the SES-07, SES-08 and SES-10 catalog statuses to `done`.
- Verification: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=sessions"` passed (10 passed, 1 skipped); the full acceptance suite passed (53 passed, 1 skipped, SES-04 `@bug-276`); `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes` passed; `bash scripts/check.sh` passed.
- Each new scenario was first run with its missing bindings and failed with Reqnroll's expected missing-step error, then passed after the minimum bindings were added. No differences from the approved plan.

## Comments

### 2026-11-09, claude-bdd-plan
Created at the owner's request to plan application test scenarios for GPT Luna. Scenarios and expectations are in `docs/specs/acceptance-scenarios.md`. The plan is awaiting approval.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.

### 2026-11-10, codex-gpt6
The approved plan omits fake prompt result control needed by SES-03/04. Initial Reqnroll attempts at SES-02 send and SES-05 delete did not complete within the headless scenario timeout; no product code was changed. Request plan review before extending the file list or marking these product flows as defects.

### 2026-11-10, codex-gpt6 — plan amendment requested
Please amend the approved plan to:
- allow `ImageGen/ImageGen.Acceptance.Tests/Support/FakePlatform.cs` to queue a `PromptAsync` result so SES-03/04 can exercise rename through the session row command;
- allow investigation and fixes in the acceptance harness/steps needed to run SES-02 (first-message title) and SES-05 (delete confirmation) without bypassing the UI/page-model flow;
- retain the current rule against changing product behavior to make an acceptance scenario pass. If investigation confirms a product/specification discrepancy, record it with `@ignore @bug-NNN` and a follow-up task.

Until this plan update is approved, the implementation remains limited to SES-01, SES-06 and SES-09.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan) — plan amendment approved
The requested amendment is approved: `Support/FakePlatform.cs` may queue a `PromptAsync` result for SES-03/04; the acceptance harness and steps may be investigated and fixed so that SES-02 and SES-05 run through the UI/page-model flow. The rule stays: no product changes to make a scenario pass; a confirmed product/specification discrepancy becomes `@ignore @bug-NNN` and a new task. SES-11 is not part of this task (task 273, after 255).

### 2026-11-11, codex-gpt6
Plan mismatch: SES-07 must change the default assistant model in Settings and verify that existing sessions keep their model while a new session receives the new default. `FakeOpenRouter` currently returns an empty `/api/v1/models` catalog, so Settings exposes only the currently selected model. Making a second model selectable requires adding configurable chat models to `ImageGen/ImageGen.Acceptance.Tests/Support/FakeOpenRouter.cs`, which is outside the approved `Files:` list and the prior amendment. Proposed plan change: add that file to the allowed files and permit a fake chat-model catalog response used by SES-07. Until the planner/owner approves this scope, this task is blocked; SES-08 and SES-10 are not being started under this plan.

### 2026-11-13, claude-planner — plan amendment
Answers the SES-07 mismatch of 2026-11-11. Verified: `Support/FakeOpenRouter.cs` returns an empty chat catalog for `/api/v1/models`, so Settings offers only the current assistant model. The `Files:` line now allows additive harness changes in `Support/FakeOpenRouter.cs` (a configurable chat-model catalog for SES-07) and `Support/AppWorld.cs`; the same allowance is given to every open acceptance task so that a missing fake response no longer blocks a task. Product code stays untouched; a product/specification discrepancy is still `@ignore @bug-NNN` plus a new task. Continue with SES-07, SES-08 and SES-10 from `main` (SES-01–06 and SES-09 are merged by PR #101). SES-04's long-title case stays `@ignore @bug-276`.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
