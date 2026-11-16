---
id: 261
status: review
assignee:
claimed:
created: 2026-11-09
---

# Acceptance scenarios: settings and appearance

## Description
Automate the “Settings and appearance” section of the catalog: SET-01 … SET-03. SET-04 (Clear logs) follows task 232. File: `Features/Settings.feature`, tag: `@settings`.

Depends on: 253.
Environment: Linux or Windows (headless).

## Acceptance criteria
- SET-01 … SET-03 are in `.feature`; each passes or is tagged `@ignore @bug-NNN` with a task.
- SET-01: a clean data folder has the Quiet style and “system” theme selected (`SettingsPageModel.Styles`, selected theme).
- SET-02: changing the style to Gallery and the theme to dark applies immediately — verify through Avalonia application resources (`Application.Current.RequestedThemeVariant` and the style token, as in `Appearance/AvaloniaAppearanceApplierTests.cs`) — and persists after `Restart`.
- SET-03: opening Settings from the sessions screen (`SessionsPageModel.OpenSettingsAsync`) navigates to `Routes.Settings`.
- Update the SET-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs` (`Styles`, `SelectThemeAsync`, `LoadAsync`), `StyleOptionViewModel.cs`; `ImageGen/ImageGen.Presentation/Appearance/` (service and settings file).
- Avalonia: `ImageGen/ImageGen.Avalonia.Tests/Appearance/AvaloniaAppearanceApplierTests.cs` — how application is verified.
- Flow tests: `Flows/SettingsFlowTests.cs`.
- Specification: `docs/specs/chat-sessions.md` S6.1–S6.4; ADR 0014.

## Plan
Implement one scenario at a time, SET-01 → SET-03.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/Settings.feature`, `Steps/SettingsSteps.cs`, `Steps/CommonSteps.cs`, `docs/specs/acceptance-scenarios.md`, `Support/FakeOpenRouter.cs` and `Support/AppWorld.cs` (additive test-harness changes only: new configurable fake responses, catalog fields, world helpers; no change of existing behavior used by other features, no product code); for this task `Support/AppWorld.cs` may also pass the real file logger (`configureLogging: logging => logging.AddImageGenFileLogging(<world data folder>/logs, ...)`, as `ImageGen/ImageGen.Avalonia/App.axaml.cs` line 58 does) and start `AvaloniaAppearanceApplier` against `Application.Current` for each started world (as `App.axaml.cs` line 78), disposing or stopping it with the world.

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=settings"
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

- Implemented SET-01 through SET-04 in `Features/Settings.feature` and `Steps/SettingsSteps.cs`. The scenarios cover the default appearance, immediate Avalonia theme/style application and persistence after restart, navigation to Settings from the sessions screen, and clearing old logs while new entries continue to be written.
- `Support/AppWorld.cs` configures the file logger and starts an `AvaloniaAppearanceApplier` for each world, as allowed by the approved plan amendment.
- Verification: the Settings category passed (4/4); the full acceptance suite passed (54 passed, 1 skipped: SES-04, tracked by task 276); formatting and `bash scripts/check.sh` passed. The first full-suite attempt timed out in SES-02; its retry passed.
- No product code changed. All four catalog entries are marked `done`.

## Comments

### 2026-11-09, claude-bdd-plan
Created at the owner’s request to plan application test scenarios for GPT Luna. Scenarios and expectations are in `docs/specs/acceptance-scenarios.md`. The plan is awaiting approval.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan) — plan extended
Task 232 is done, so SET-04 is added to this task: "Clear logs" (`SettingsPageModel.ClearLogsAsync`, confirmation through the dialog fake) deletes the log files of `<data folder>/logs`, and the app keeps writing the log afterwards (a new line appears after the next action). Files: the same as in the plan. Expectation from the catalog row SET-04 and task 232's "Report"; on a discrepancy — `@ignore @bug-NNN` and a task. Plan approved.

### 2026-11-12, codex-gpt6
The planned acceptance scenarios cannot verify the stated behavior with the current harness. `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorld.cs` creates the composition without `configureLogging`, so the app does not write files under the data directory's `logs/`. It also does not start `AvaloniaAppearanceApplier`; only `ImageGen/ImageGen.Avalonia/App.axaml.cs` does that, and the plan forbids product/head startup changes. Therefore SET-02 cannot observe the live theme/style resources after the user's Settings command, and SET-04 cannot clear and then observe the active log file continuing to receive entries.

Proposed plan amendment: add `Support/AppWorld.cs` to `Files:` and allow it to configure the real file logger for the acceptance composition and start the Avalonia appearance applier against `Application.Current` for each started world. Keep the Settings steps as user actions and verify the resulting Avalonia resources and log file after restart. No implementation was made pending planner/owner approval.

### 2026-11-13, claude-planner — plan amendment
Answers the mismatch of 2026-11-12. Verified: `AppWorld` calls `AppServices.Create` without `configureLogging`, and only `App.axaml.cs` starts `AvaloniaAppearanceApplier`. The amendment lets `Support/AppWorld.cs` mirror those two startup steps of the real app (harness only, no head or product change), so SET-02 observes the applied theme resources and SET-04 observes the log files being cleared and written again. If starting the applier for every world leaks resources between scenarios (theme of one scenario visible in the next), reset it in the world's disposal; if that is impossible, `blocked` with the evidence.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
