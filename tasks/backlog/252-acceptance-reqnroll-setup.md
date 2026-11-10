---
id: 252
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-10T08:46:57Z
created: 2026-11-09
---

# Acceptance scenarios: Reqnroll, the project and headless Avalonia

## Description
The owner's request of 2026-11-09: adopt "something like Cucumber" for tests. The decision is ADR 0031 (Reqnroll on xUnit v3, headless Avalonia, English Gherkin), the scenario catalog is `docs/specs/acceptance-scenarios.md`.

This task creates an empty but working skeleton: the project `ImageGen/ImageGen.Acceptance.Tests`, Reqnroll, starting the Avalonia headless session from Reqnroll hooks, one end-to-end scenario KEY-01 ("first launch without a key → the key screen") and the CI step. The world with the scenario network fake is task 253.

Unknown (2026-11-09), find out in the first step:
- whether `Reqnroll.xunit.v3` 3.3.4 works with `xunit.v3` 4.0.2 from `Directory.Packages.props`. `Avalonia.Headless.XUnit` 12.1.4 does not work with 4.x, so `ImageGen.Avalonia.Tests` is pinned to 3.2.2 (`VersionOverride`, ADR 0007);
- whether Reqnroll bindings can run code on the Avalonia thread through `HeadlessUnitTestSession` (the `Avalonia.Headless` package, without `Avalonia.Headless.XUnit`, because Reqnroll generates `[Fact]` itself, not `[AvaloniaFact]`).

Depends on: —
Environment: Linux or Windows (headless, no display).

## Acceptance criteria
- Step 1, proof-of-concept code in `poc/backlog/252-acceptance-reqnroll-setup/` (rules in `poc/README.md`): a minimal project with one English scenario that starts a `HeadlessUnitTestSession` with an `AppBuilder` like `TestAppBuilder` in a hook and checks in a step that the code runs on the UI thread (`Dispatcher.UIThread.CheckAccess()`). Check with `xunit.v3` 4.0.2 and, if it does not work, with 3.2.2. The conclusion (versions, what works, the error text) goes into "Comments" and a new file `docs/knowledge/acceptance-tests.md`.
  - If no version works — `blocked`, a question to the owner: move to `Reqnroll.NUnit` (ADR 0031, item 1)?
- The project `ImageGen/ImageGen.Acceptance.Tests` (`net10.0`, `OutputType` as in `ImageGen.Avalonia.Tests`), a reference to `ImageGen.Avalonia`; the packages `Reqnroll.xunit.v3`, `Avalonia.Headless` (versions in `Directory.Packages.props`), the xunit.v3 version per step 1 with a comment in the csproj, as in `ImageGen.Avalonia.Tests`.
- `reqnroll.json`: the default (English) feature language, bindings from the project assembly. Layout: `Features/`, `Steps/`, `Support/`.
- `Support/HeadlessApp` (the name per step 1): one headless session per run (the `[BeforeTestRun]` hook), stopped in `[AfterTestRun]`, a method that runs a step's asynchronous code on the UI thread with a timeout. The data folder is temporary, as in `ImageGen.Avalonia.Tests/TestAppBuilder.cs` (the real folder and key store are never opened, task 048).
- Scenario KEY-01 in `Features/FirstRunAndKeys.feature` is green. For it the composition `AppServices.Create` with an OpenRouter stub and substituted dialogs (as in `ShellHarness`) is enough; the full world is in 253, do not build it here.
- A self-check (an ordinary xUnit test in the project, not Gherkin): a Reqnroll scenario with an undefined step fails instead of being skipped (`missingOrPendingStepsOutcome` = `Error`).
- Scenarios run sequentially (one headless session per process); xUnit parallelism is off for the project.
- CI: a `dotnet test` step for the project in the `avalonia` job of `.github/workflows/build.yml`, after the Avalonia tests (the owner allowed this change on 2026-11-09). Also in the `windows-tests` job, if the project passes on Windows locally in CI; otherwise record why in the "Report".
- Documentation:
  - `AGENTS.md`, "Commands": the command already lists the project (added 2026-11-09) — remove "after task 252";
  - `docs/conventions.md`, "Tests": already describes Reqnroll — check that it matches what was built;
  - ADR 0031: add the found xunit.v3 version with a date to "Decision";
  - `docs/index.md`: a line for `knowledge/acceptance-tests.md`.
- `ImageGen.slnx` contains the project.
- Everything new is in English (`docs/conventions.md`, "Language"); `scripts/check.sh` passes.

## Analysis
- The composition and headless model: `ImageGen/ImageGen.Avalonia.Tests/TestAppBuilder.cs` (a temporary folder, `App.HostOverrides`), `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs` (`AppServices.Create`, `AvaloniaNavigationService.AttachHost`, `StartAsync`, `WaitUntilAsync`), `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/TempAppPaths.cs`, `StubHttpMessageHandler.cs`, `OpenRouterFakes.cs`.
- Scenario KEY-01 is already checked in code in `ImageGen/ImageGen.Avalonia.Tests/Flows/StartFlowTests.cs` — compare the expectations; do not delete the flow test (that is task 267).
- Do not move the doubles from `ImageGen.Avalonia.Tests` in this task: a test project does not reference a test project. What KEY-01 needs is a minimal copy in `Support/` marked "replaced by the world of 253".
- `ImageGen.Avalonia.Tests.csproj` explains the xunit.v3 3.2.2 pin — Reqnroll has the same risk.

## Plan
1. POC (step 1), the conclusion into "Comments" and `docs/knowledge/acceptance-tests.md`; on failure — `blocked`.
2. The project, packages, `reqnroll.json`, the solution; an empty `dotnet test` run (0 tests) passes.
3. The self-check "an undefined step is an error" (the test first, then the setting).
4. `HeadlessApp` and the hooks; KEY-01: the `.feature` first (it fails — the steps are not bound), then the bindings.
5. The CI step; documentation.

Files:
- `poc/backlog/252-acceptance-reqnroll-setup/` (new);
- `ImageGen/ImageGen.Acceptance.Tests/` (new): `ImageGen.Acceptance.Tests.csproj`, `reqnroll.json`, `Features/FirstRunAndKeys.feature`, `Steps/FirstRunSteps.cs`, `Support/HeadlessApp.cs`, `Support/Hooks.cs`, `ReqnrollSetupTests.cs`;
- `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `.github/workflows/build.yml`;
- `AGENTS.md`, `docs/conventions.md`, `docs/decisions/0031-bdd-acceptance-reqnroll.md`, `docs/knowledge/acceptance-tests.md` (new), `docs/index.md`, `docs/specs/acceptance-scenarios.md` (the KEY-01 status).

### Verification
```
dotnet build ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
# in GitHub Actions: build.yml, the avalonia job is green with the new step
```

### Execution order (for Luna)
- First read only the files of the "Files" line and the "Analysis" section; check signatures against the current `main`.
- Step 1 (POC) is mandatory before any code in `ImageGen/`. Do not invent package versions: take them from nuget.org and write down where from.
- Steps strictly in order (ub-mode): a failing test → run it, make sure it fails exactly for the described reason → minimal code → the test is green.
- Do not change files outside the "Files" line; do not touch `ImageGen.Avalonia.Tests`.
- If the code differs from the plan, do not improvise: stop and write a question in "Comments".
- Run `dotnet` commands one at a time, not in parallel. After each changed project — `dotnet format <project> --verify-no-changes`.
- Write everything in English. In the "Report": what changed, package versions, commands and results, differences from the plan.

## Report

### 2026-11-10, codex-gpt6 (in progress)
- Added the proof of concept under `poc/backlog/252-acceptance-reqnroll-setup/`. `dotnet test` passed with `Reqnroll.xunit.v3` 3.3.4, `Avalonia.Headless` 12.1.4 and both `xunit.v3` 4.0.2 and 3.2.2. The project uses 4.0.2 without a version override.
- Added `ImageGen.Acceptance.Tests`, its Reqnroll configuration, a serial headless session, timeout-bound UI-thread dispatch, temporary data paths, an in-memory key store, a stub HTTP handler, and substitute dialogs. KEY-01 passes against the real Avalonia composition.
- Added an ordinary xUnit self-check for `missingOrPendingStepsOutcome = Error`. Before the step bindings were added, the KEY-01 scenario failed with `Reqnroll.MissingStepDefinitionException`, confirming that undefined steps fail the run rather than being skipped.
- Configured Reqnroll to generate feature code-behind files under `obj/`, not beside tracked `.feature` files.
- Added the project to `ImageGen.slnx` and to Linux `avalonia` and Windows `windows-tests` CI jobs. Updated the task command in `AGENTS.md`, the conventions, ADR 0031, the scenario catalog and the knowledge index.
- `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes` — passed. Debug and Release builds passed; Release acceptance tests — 2 passed. `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj` — 180 passed, 2 skipped (D-Bus unavailable in this session; DPAPI is Windows-only). `bash scripts/check.sh` — passed.
- The POC showed that `[AfterTestRun]` may run on the Avalonia dispatcher thread. Disposing the session synchronously there deadlocks, so the hook disposes it on a worker thread; this implementation detail is recorded in `docs/knowledge/acceptance-tests.md`.
- GitHub Actions validation, including Windows, is pending; the task remains `in-progress` until it passes.

## Comments

### 2026-11-09, claude-bdd-plan
Created at the owner's request "let's adopt something like Cucumber" and "plan them as tasks for GPT Luna". The Reqnroll 3.3.4 and Allure.Reqnroll 2.15.0 versions were checked on nuget.org on 2026-11-09; compatibility with xunit.v3 4.0.2 was not checked — the first step.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved. The acceptance tests go to CI (the change of `.github/workflows/build.yml` is allowed). Everything in English, including the Gherkin. The task was rewritten in English accordingly: English Gherkin instead of `# language: ru`, the CI step is no longer conditional; status `approved`.

### 2026-11-10, codex-gpt6
The required POC passes with `Reqnroll.xunit.v3` 3.3.4, `Avalonia.Headless` 12.1.4 and both `xunit.v3` 4.0.2 and 3.2.2. In both cases a Reqnroll step dispatched through `HeadlessUnitTestSession` observes `Dispatcher.UIThread.CheckAccess() == true`. The run also exposed a lifecycle issue: the after-run hook can execute on the Avalonia dispatcher thread, where synchronous `Dispose()` deadlocks; dispose on a worker thread. Details and package sources are in `docs/knowledge/acceptance-tests.md`.
