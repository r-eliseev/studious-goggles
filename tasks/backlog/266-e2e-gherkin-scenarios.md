---
id: 266
status: blocked
assignee:
claimed:
created: 2026-11-09
---

# UI tests of the built apps: migrate to Gherkin scenarios on Reqnroll

## Description
The owner's decision of 2026-11-09: "we are essentially replacing the current e2e tests with these; the current tests must be migrated to the new framework" (ADR 0031, item 8). The UI tests of the built MAUI apps (`ImageGen.E2E.Android`, `ImageGen.E2E.Windows`, NUnit + Allure, ADR 0029) are rewritten as scenarios of the catalog `docs/specs/acceptance-scenarios.md` with the `@e2e` tag and run through `Reqnroll.NUnit` + `Allure.Reqnroll`. The drivers (Appium/UiAutomator2, FlaUI), the per-test video, evidence and the `ui-e2e.yml` workflow stay; only the way tests are written changes.

Assumption (open question in `docs/status.md`): the Appium/FlaUI runs of the built apps stay; "replacing" means the test framework, not dropping device runs. If the owner says otherwise, this task is re-planned.

What is migrated now:
- `FirstRunTests.Launch_ShowsFirstRunKeyScreen` (Android, Windows) → KEY-01;
- `FirstRunTests.InvalidKey_ShowsError` (Android, Windows) → KEY-03;
- `FirstRunTests.Rotate_KeepsKeyScreen` (Android) → KEY-11 (`@e2e-only`), with the step order fixed by task 251;
- after 243: `MockSessionTests.FirstRunWithMock_GeneratesImage` → CHAT-02 and GEN-05.

Unknown (2026-11-09), find out in step 1:
- whether `Allure.Reqnroll` 2.15.0 works together with `Allure.NUnit` 2.15.0 in one NUnit project (both write `allure-results`), or `Allure.NUnit` has to be removed;
- how to include the shared `.feature` files of `ImageGen.Acceptance.Tests/Features` into the E2E projects (MSBuild `<ReqnrollFeatureFiles Include=... Link=...>` or `<None Include=... Link=...>`) so that Reqnroll generates the tests only for `@e2e` scenarios (a tag filter at generation, or `@ignore` for non-e2e at run time — whichever Reqnroll supports);
- whether `Reqnroll.NUnit` 3.3.4 works with `NUnit` 4.6.1 from `Directory.Packages.props`.

Depends on: 251 (the final-state evidence and the rotation order), 254 (KEY scenarios exist in the catalog and the feature file). CHAT-02/GEN-05 additionally after 243.
Environment: Linux (building the E2E projects), GitHub Actions (the run).

## Acceptance criteria
- Step 1 (facts) in "Comments" and `docs/knowledge/ui-e2e.md`; if Reqnroll cannot work in the E2E projects — `blocked` with a question to the owner.
- `ImageGen.E2E.Android` and `ImageGen.E2E.Windows` reference `Reqnroll.NUnit` and `Allure.Reqnroll` (versions in `Directory.Packages.props`), link the shared feature files and contain step bindings over the existing fixtures (`AndroidTestBase`/`AndroidSession`/`TestArtifacts`, `WindowsTestBase`/`VideoRecorder`). Bindings find elements only by `TestIds` (`ImageGen.E2E.Common/TestIds.cs`).
- Per-test evidence keeps working per scenario: a video, `screenshot.png` (always, after 251), the UI tree on failure, logs, `metadata.json`; the Allure report shows the Gherkin steps and the attachments. The `reports/<platform>/<scenario>/` folder is named after the scenario id (`KEY-01`) instead of `<Class>.<Test>`.
- The step texts are the same as in the headless bindings (the same `.feature` files); E2E-specific steps (rotation) are tagged `@e2e-only`, and the headless run skips them.
- The old NUnit test classes `FirstRunTests` (both projects) are deleted once the same scenarios are green in `ui-e2e.yml`; `VideoRecorderTests` stays (a unit test of the recorder, not a user scenario).
- `ui-e2e.yml` runs the Reqnroll tests; the job names and the separate PR checks do not change.
- `docs/specs/ui-e2e.md`, ADR 0029 (a dated line in "Decision") and the catalog statuses (`@e2e` rows) are updated.

## Analysis
- E2E code: `ImageGen/ImageGen.E2E.Android/{FirstRunTests,AndroidTestBase,AndroidSession,TestArtifacts}.cs`, `ImageGen/ImageGen.E2E.Windows/{FirstRunTests,WindowsTestBase,VideoRecorder,VideoRecorderTests}.cs`, `ImageGen/ImageGen.E2E.Common/TestIds.cs`, `ImageGen/ImageGen.E2E.Common/ci/`, `.github/workflows/ui-e2e.yml`.
- Overlap: 251 changes `AndroidTestBase`, `TestArtifacts`, `FirstRunTests`, `WindowsTestBase` — 251 goes first; 243 (in progress, codex-gpt6) adds `MockSessionTests` — migrate it after 243 is merged, in this task if 243 is done by then, otherwise as a follow-up comment.
- Apple E2E (248, 250) use the same NUnit + Allure stack; when they are implemented, they are written in Gherkin from the start (a comment in 248).

## Plan
1. Step 1 facts (a POC in `poc/backlog/266-e2e-gherkin-scenarios/` if needed).
2. Android: packages, linked features, bindings for KEY-01, KEY-03, KEY-11; evidence hooks (`[BeforeScenario]`/`[AfterScenario]` calling the existing fixture code); a CI run; compare the evidence with the old run; delete `FirstRunTests`.
3. Windows: the same for KEY-01, KEY-03.
4. After 243: CHAT-02, GEN-05 on both platforms.
5. Documentation.

Files: `ImageGen/ImageGen.E2E.Android/` (`ImageGen.E2E.Android.csproj`, `Steps/` new, `Hooks.cs` new, `FirstRunTests.cs` deleted, `AndroidTestBase.cs`, `TestArtifacts.cs`), `ImageGen/ImageGen.E2E.Windows/` (`ImageGen.E2E.Windows.csproj`, `Steps/` new, `Hooks.cs` new, `FirstRunTests.cs` deleted, `WindowsTestBase.cs`), `ImageGen/ImageGen.Acceptance.Tests/Features/FirstRunAndKeys.feature` (tags only), `ImageGen/Directory.Packages.props`, `.github/workflows/ui-e2e.yml`, `docs/specs/ui-e2e.md`, `docs/decisions/0029-ui-e2e-tests.md`, `docs/knowledge/ui-e2e.md`, `docs/specs/acceptance-scenarios.md`.

### Verification
```
dotnet build ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj
dotnet build ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj --verify-no-changes
bash scripts/check.sh
# in GitHub Actions: ui-e2e green, the Allure report shows the Gherkin steps, the videos end on the checked state
```

### Execution order (for Luna)
- First read only the files of the "Files" line and the named tests; check signatures against the current `main`.
- Steps strictly in order (ub-mode): a failing test → run it, make sure it fails exactly for the described reason → minimal code → the test is green.
- Do not change files outside the "Files" line, do not rename members beyond the plan, do not fix neighboring findings — they go to separate tasks.
- If the code differs from the plan, do not improvise: stop and write a question in "Comments".
- Run `dotnet` commands one at a time, not in parallel. After each changed project — `dotnet format <project> --verify-no-changes`.
- Write everything in English. In the "Report": what changed, commands and results, differences from the plan.

## Report

## Comments

### 2026-11-09, claude-bdd-plan
Rewritten after the owner's decision "migrate the current e2e tests to the new framework". The plan waits for approval.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan)
Plan approved ("approve all tasks with plans").

### 2026-11-12, codex-gpt6
Step 1 POC: `dotnet test poc/backlog/266-e2e-gherkin-scenarios/ReqnrollAllurePoc.csproj` passes both tagged scenarios with Reqnroll.NUnit 3.3.4, NUnit 4.6.1, Allure.NUnit 2.15.0 and Allure.Reqnroll 2.15.0; both are discovered (including `@e2e-only`) and produce Allure result files. `@e2e-only` therefore does not automatically skip a scenario in the headless run. A linked `ReqnrollFeatureFiles` item is processed to intermediate `.ndjson`, but the POC did not confirm generated test code for that linked shared feature.

The approved plan cannot be implemented as written: `FirstRunAndKeys.feature` is the only shared feature permitted in Files, but the migrated CHAT-02 and GEN-05 scenarios live in `ChatAssistant.feature` and `Runs.feature`; neither is listed. The plan also requires the headless run to skip KEY-11 (`@e2e-only`), while its permitted files do not include the acceptance test project or its configuration, where the POC proves a skip must be implemented. Proposed plan change: permit tag-only edits to `ChatAssistant.feature` and `Runs.feature` to tag CHAT-02/GEN-05 `@e2e`; permit the minimal acceptance-project configuration/binding change that excludes `@e2e-only` only in headless runs; and clarify the MSBuild feature inclusion item/output expected for linked shared features. Blocked and claim released pending planner response.
