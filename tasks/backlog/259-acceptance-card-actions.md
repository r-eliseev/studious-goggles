---
id: 259
status: approved
assignee:
claimed:
created: 2026-11-09
---

# Acceptance scenarios: generation card actions

## Description
Automate the “Действия над генерацией” section of the catalog: ACT-01 … ACT-08. File `Features/CardActions.feature`, tag `@actions`.

Depends on: 253, 257.
Environment: Linux or Windows (headless).

## Acceptance criteria
- Add ACT-01 … ACT-08 to `.feature`; each scenario passes or has `@ignore @bug-NNN` and a tracking task.
- ACT-01/02: on a completed card, `RepeatAsync` and `NewSeedAsync` each create a new run with one generation in the timeline. Keep the same model and prompt (request body); use the same seed for Repeat or the next seed from the seed generator for New seed. Do not call the assistant (the fake log has no new chat completion).
- ACT-03: `EditAsync` opens an edit dialog (the dialog fake returns corrected text), then starts a run with the corrected prompt and no assistant call.
- ACT-04: on a failed card, `OtherModelAsync` opens the model picker (`ModelPickerPageModel`), with this session's models at the top of `UsedModels`; selecting a model (`PickAsync`) starts a run with that model and the same prompt.
- ACT-05: `CopyPromptAsync` sends the exact prompt to the clipboard fake.
- ACT-06: `DetailsAsync` on a failed card opens a dialog with the original error text from the fake response body; the card shows a short reason (`ErrorText`), not the original text.
- ACT-07: `SaveAsync` on a completed card sends the filename `imagegen-<first 8 id characters>.png` to the `IImageSaver` fake.
- ACT-08, `Scenario Outline`: identify actions available for “готово”, “ошибка”, “отфильтровано”, and “отменено” states (table based on specification S4.5–S4.8). Determine availability from the user's card menu (`ShowActionsAsync` / action set in code); see `Views/CardActionsTests.cs` for how to verify it.
- Update ACT-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` (`Repeat`, `NewSeed`, `Edit`, `Save`, `Details`, `OtherModel`, `CopyPrompt`, `ShowActions` commands), `ModelPickerPageModel.cs`, `CopyTextAction.cs`.
- `ImageGen/ImageGen.Presentation/Services/ImageFileNames.cs` (filename), `IImageSaver.cs`, `IClipboardService.cs`, `IDialogService.cs`.
- Flow tests: `Flows/OtherModelFlowTests.cs`, `Views/CardActionsTests.cs`, `Flows/ChatFlowTests.cs` (`Repeat_AfterFinish_StartsSecondRun`).
- Specification: `docs/specs/chat-sessions.md` S4.1–S4.8; `docs/specs/image-client.md` F5.5, F6.4, F9.1.

## Plan
Implement scenarios ACT-01 → ACT-08 one at a time.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/CardActions.feature`, `Steps/CardActionsSteps.cs` (new), `Steps/CommonSteps.cs`, `Support/FakePlatform.cs` (only new fake responses), `docs/specs/acceptance-scenarios.md`, `Support/FakeOpenRouter.cs`, `Support/FakePlatform.cs` and `Support/AppWorld.cs` (additive test-harness changes only, amendment of 2026-11-13).

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=actions"
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
