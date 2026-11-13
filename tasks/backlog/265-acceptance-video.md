---
id: 265
status: approved
assignee:
claimed:
created: 2026-11-09
---

# Acceptance scenarios: video

## Description
Automate the “Видео” section of the catalog: VID-01 … VID-04. Add VID-05 (audio) after 221–223. File `Features/Video.feature`, tag `@video`.

Depends on: 253, 257. If tasks 218–220 (MAUI video player and card) change `GenerationItemViewModel` when this task is claimed, compare its members with `main`.
Environment: Linux or Windows (headless).

## Acceptance criteria
- Step 1: implement video routes in `FakeOpenRouter` based on `OpenRouterClient` (`GET videos/models`, `POST videos`, job polling, `GET videos/{id}/content`). Derive response shapes only from the code and `docs/knowledge/media-generation.md`; if information is missing, ask in “Comments”.
- Add VID-01 … VID-04 to `.feature`; each scenario passes or has `@ignore @bug-NNN` and a tracking task.
- VID-01: without selected video models, the chat completion body has no `generate_video` tool; with a selected model, it does.
- VID-02: calling `generate_video` creates an `IsVideo` card; after polling (time via `FakeTimeProvider`, waiting via `IAsyncDelay`, as decided in 258), status becomes “готово” and the video file is in the data folder.
- VID-03: a content download error results in “ошибка” and no file (S “Ошибка скачивания или сохранения …” in `media-generation.md`).
- VID-04: cancelling a video results in “отменено” and a warning that the provider may continue (use resources).
- Do not test the player in headless mode (`UnsupportedMediaPlayerService`).
- Update the VID-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs` (around lines 82, 161, 171), `ImageGen/ImageGen.Core/Services/VideoModelCatalogService.cs`, `VideoPriceCalculator.cs`; `SessionModelsPageModel.ShowVideoModels`, `VideoModels`; `GenerationItemViewModel.IsVideo`, `VideoFilePath`.
- Specification: `docs/specs/media-generation.md`, “Требования”; ADR 0027.

## Plan
1. Add video routes to the fake (start with self-checks in `FakeNetworkTests`).
2. Implement scenarios VID-01 → VID-04 one at a time.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/Video.feature`, `Steps/VideoSteps.cs` (new), `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs`, `Support/FakeNetworkTests.cs`, `docs/specs/acceptance-scenarios.md`, `Support/FakeOpenRouter.cs`, `Support/FakePlatform.cs` and `Support/AppWorld.cs` (additive test-harness changes only, amendment of 2026-11-13).

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=video"
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

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan) — plan extended
Tasks 221–223 are done, so VID-05 (audio) is added to this task: with an audio model selected in the session, the assistant gets the `generate_audio` tool (request body), the call creates an audio card (`IsAudio`) that becomes done with an audio file in the data folder; without an audio model the tool is not offered. The fake's audio route follows the code of the OpenRouter audio client (task 221: `chat/completions` with an audio stream) — take the shape from the code and `docs/knowledge/media-generation.md`, not from memory. Expectations from `docs/specs/media-generation.md` (the audio requirements). Split into separate catalog rows (VID-05, VID-06, …) as needed. Plan approved.

### 2026-11-13, claude-planner — plan amendment
Preventive amendment for all open acceptance tasks: tasks 255, 256 and 261 were blocked only because a needed fake response or world helper was outside their `Files:` line. Additive changes in `Support/FakeOpenRouter.cs`, `Support/FakePlatform.cs` and `Support/AppWorld.cs` are now allowed (new configurable responses, catalog fields, world helpers; no change of behavior other features rely on; no product code). Product/specification discrepancies are still `@ignore @bug-NNN` plus a new task. If two open tasks need the same harness change, the second one reuses it from `main`.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
