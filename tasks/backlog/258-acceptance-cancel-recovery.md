---
id: 258
status: approved
assignee:
claimed:
created: 2026-11-09
---

# Acceptance scenarios: cancelling generations and recovery after restart

## Description
Automate the “Отмена и восстановление” section of the catalog: CAN-01 … CAN-05. File `Features/CancelAndRecovery.feature`, tag `@cancel`.

Depends on: 253, 257 (run steps and holding responses).
Environment: Linux or Windows (headless).

## Acceptance criteria
- Add CAN-01 … CAN-05 to `.feature`; each scenario passes or has `@ignore @bug-NNN` and a tracking task.
- CAN-01: hold two generations; cancel one (`GenerationItemViewModel.CancelAsync`) → it becomes “отменено”; after releasing the other, it becomes “готово”.
- CAN-02: cancel a run (`RunItemViewModel.CancelRunAsync`) with one completed and two held generations → the completed one is unchanged and the other two become “отменено”.
- CAN-03: the provider response arrives before cancellation is confirmed (steps set the order) → the card becomes “готово” and the image is saved.
- CAN-04: hold a generation, then restart the world → the card becomes “ошибка” with reason “Interrupted” (per resources). Under S3.8, OpenRouter without a job handle becomes Interrupted; queued providers with handles are not covered here.
- CAN-05: the cancellation confirmation time (from `FakeTimeProvider`) appears on the card (`CancellationStatusText`) and remains after `Restart`.
- No scenario waits for real time: advance fake timeouts and polling with `FakeTimeProvider.Advance` when code uses `TimeProvider`. If code waits on `Task.Delay` without `TimeProvider` (`IAsyncDelay`, `SystemAsyncDelay`), record it in “Report” and replace `IAsyncDelay` through the world seam if necessary.
- Update CAN-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` (`CancelAsync`, `IsCancelled`, `CancellationStatusText`, `HasCancellationStatus`), `RunItemViewModel.cs` (`CancelRunAsync`).
- Core: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `GenerationCancelResult.cs`, `IAsyncDelay.cs`; startup recovery is in `ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`.
- Specification: `docs/specs/chat-sessions.md` S3.5.1–S3.5.3, S3.8; `docs/specs/image-client.md` F5.4.

## Plan
Implement scenarios CAN-01 → CAN-05 one at a time. For CAN-03 event ordering, use explicit “provider responds to generation N” and “user cancels generation N” steps, with no time delays.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/CancelAndRecovery.feature`, `Steps/RunsSteps.cs`, `Steps/CommonSteps.cs`, `Support/AppWorld.cs` (only replace `IAsyncDelay` if needed), `docs/specs/acceptance-scenarios.md`, `Support/FakeOpenRouter.cs`, `Support/FakePlatform.cs` and `Support/AppWorld.cs` (additive test-harness changes only, amendment of 2026-11-13).

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=cancel"
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
(The tag filter applies if Reqnroll passes tags as xUnit categories; task 252 records how this works in `docs/knowledge/acceptance-tests.md`.)

### Execution order (for Luna)
- First read only the files in the “Files” line, the named tests, and sections of `docs/specs/acceptance-scenarios.md`; compare signatures with current `main`.
- Follow steps in order (ub-mode): add a scenario to `.feature`, run it and confirm it fails because the step is unbound or the expectation is unmet; add the minimal binding; get a passing scenario. One scenario per commit (include the scenario id in the commit message).
- Derive scenario expectations from the specification, not the code. If the code behaves differently, do not weaken the scenario or fix code: add `@ignore @bug-NNN`, create a task with reproduction steps using `scripts/new-task.sh --publish backlog <name>`, and set the catalog status to “расхождение (NNN)”.
- Steps act as a user: use page model commands, select items, and navigate. Do not call repositories, Core services, or SQL directly, except in “Given” steps that prepare the world and in “file exists” checks.
- Add shared steps to `Steps/CommonSteps.cs`; first check whether a step already exists with different wording.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature), do not improvise; stop and write a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the “Report”, list passing scenarios, ignored scenarios and their tracking tasks, commands and results, and differences from the plan. Update scenario statuses in `docs/specs/acceptance-scenarios.md`.

## Report

## Comments

### 2026-11-09, claude-bdd-plan
Created at the owner's request to plan application test scenarios for GPT Luna. Scenarios and expectations are in `docs/specs/acceptance-scenarios.md`. The plan awaits approval.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.

### 2026-11-13, claude-planner — plan amendment
Preventive amendment for all open acceptance tasks: tasks 255, 256 and 261 were blocked only because a needed fake response or world helper was outside their `Files:` line. Additive changes in `Support/FakeOpenRouter.cs`, `Support/FakePlatform.cs` and `Support/AppWorld.cs` are now allowed (new configurable responses, catalog fields, world helpers; no change of behavior other features rely on; no product code). Product/specification discrepancies are still `@ignore @bug-NNN` plus a new task. If two open tasks need the same harness change, the second one reuses it from `main`.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
