---
id: 262
status: in-progress
assignee: codex-20261113
claimed: 2026-11-13T19:35:14Z
created: 2026-11-09
---

# Acceptance scenarios: spending

## Description
Automate the “Расходы” section of the catalog: SPD-01 … SPD-05. File `Features/Spending.feature`, tag `@spending`.

Depends on: 253, 257 (run steps).
Environment: Linux or Windows (headless).

## Acceptance criteria
- Add SPD-01 … SPD-05 to `.feature`; each scenario passes or has `@ignore @bug-NNN` and a tracking task.
- Create spending data through real runs with a price from the fake (`usage.cost`), not by writing directly to the database.
- SPD-01: two generations priced at 0.014 and 0.03 → daily total 0.044 (format according to `SpendingPageModel.TotalText`, invariant culture), grouped by provider and model (`Groups`).
- SPD-02: a generation without `usage.cost` is excluded from the total and included in `UnknownOperationsText`.
- SPD-03: set the time zone and “сейчас” with `FakeTimeProvider` (`SetLocalTimeZone` if needed); run on Sunday, then advance “сейчас” to Monday → it is excluded from `Week` and included in `Month` (if still the same month).
- SPD-04: the threshold is off by default; after setting a threshold below the next run's estimate, the chat shows a warning before sending (derive exact behavior from `ChatPageModel` and F8.4).
- SPD-05: the monthly limit (`MonthlyLimitText`, `SaveSettingsAsync`) persists after `Restart`.
- Update SPD-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/SpendingPageModel.cs` (`SpendingPeriod` Day/Week/Month, `Groups`, `TotalText`, `UnknownOperationsText`, `MonthlyLimitText`, `ExpensiveRunThresholdText`, `SaveSettingsAsync`; accepts `TimeProvider`), `ImageGen/ImageGen.Presentation/Spending/SpendingService.cs`.
- Entry point: `SettingsPageModel.OpenSpendingAsync`.
- Unit tests: `ImageGen/ImageGen.Presentation.Tests/Spending/`; compare expectations.
- Specification: `docs/specs/image-client.md` F8.1–F8.4.

## Plan
Implement scenarios SPD-01 → SPD-05 one at a time. Add a “Given now <date time> <time zone>” step to `CommonSteps` if task 255 has not added it already.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/Spending.feature`, `Steps/SpendingSteps.cs` (new), `Steps/CommonSteps.cs`, `docs/specs/acceptance-scenarios.md`, `Support/FakeOpenRouter.cs`, `Support/FakePlatform.cs` and `Support/AppWorld.cs` (additive test-harness changes only, amendment of 2026-11-13).

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=spending"
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
