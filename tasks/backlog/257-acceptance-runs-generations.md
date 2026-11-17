---
id: 257
status: done
assignee:
claimed:
created: 2026-11-09
---

# Acceptance scenarios: runs and generations

## Description
Automate the “Runs and generations” section of the catalog: GEN-01 … GEN-11. File: `Features/Runs.feature`, tag: `@runs`.

Depends on: 253; use chat steps from 256 (if 256 has not merged yet, use shared message-sending steps from `CommonSteps` instead of duplicating them).
Environment: Linux or Windows (headless).

## Acceptance criteria
- GEN-01 … GEN-11 are in `.feature`; each passes or is tagged `@ignore @bug-NNN` with a task.
- GEN-01: a step sets the seed source (“Given the seed source returns 11, 22”); both models receive 11 and 22 in request bodies, and four cards appear.
- GEN-02: a model without seed support in the catalog (`supported_parameters` omits seed) sends no seed field; its card says “seed not supported” (from resources).
- GEN-03/04: outcomes for two models in one run are independent; check states using `GenerationItemViewModel.StatusKind` and `StatusText`; a filtered item has `IsFiltered`, not an error.
- GEN-05: the fake holds the generation response → the card is `IsActive`; when the step says “the provider responds” → it is `IsDone` with an image.
- GEN-06: the image file exists in the data folder before the step checks the card; after `Restart`, the same card shows the same file.
- GEN-07: the fake response’s `usage.cost` appears on the card; formatting follows `ImagePriceText` and uses invariant culture.
- GEN-08: the run summary updates as generations complete (two generations, one held: first “1 of 2”, then “2 of 2”; use resource wording).
- GEN-09: a 120×80 PNG from the fake → the card shows `120×80 px · 3:2`.
- GEN-10: changing session models during a held run does not change that run’s card count; the next run uses the new models.
- GEN-11: the next-run estimate (`ChatPageModel.NextRunEstimateText`) is the sum of known prices for selected models (prices from the fake catalog).
- Update the GEN-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/RunItemViewModel.cs`, `GenerationItemViewModel.cs`, `SummaryMarkerViewModel.cs`, `ImagePriceText.cs`, `CardImageLayout.cs`, `ChatPageModel.cs`.
- Core: `ImageGen/ImageGen.Core/Services/GenerationService.cs` (`WhenIdleAsync`), `GenerationParameterPlanner.cs`, `CostEstimator.cs`, `ISeedSource.cs`.
- Flow tests: `Flows/ChatFlowTests.cs`, `Views/GenerationCardMetadataTests.cs`, `Views/SummaryMarkerTests.cs`.
- Specifications: `docs/specs/chat-sessions.md` S3.1–S3.10; `docs/specs/image-client.md` F4.4, F5.2, F6.5, F7.2, F9.2.

## Plan
Implement one scenario at a time, GEN-01 → GEN-11. Use a `DataTable` for model and outcome tables (`| model | outcome |`). The “Given these models are selected in the session: …” step selects models through the session models screen (as a user would), rather than writing settings.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/Runs.feature`, new `Steps/RunsSteps.cs`, `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs` (new responses only), `docs/specs/acceptance-scenarios.md`, `Support/FakeOpenRouter.cs` and `Support/AppWorld.cs` (additive test-harness changes only: new configurable fake responses, catalog fields, world helpers; no change of existing behavior used by other features, no product code).

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=runs"
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

- GEN-01 is implemented. The scenario selects two image models and two runs per model through the session models page, configures seeds 11 and 22, then checks four cards and both request seeds per model.
- The fake returns an empty video catalog so the session models screen completes loading, and a tool-capable default assistant model so its save command can preserve the session assistant setting.
- Verification: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=runs"` passed (1/1); `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes` passed.
- GEN-02 is implemented with a fake model whose catalog omits `seed`; two requests are checked for no `seed` property and both cards show `seed not supported`. The filtered runs acceptance test passed (2/2); formatting passed.
- GEN-03 is implemented; both failed cards for one model show the failed state/reason while both cards for the other model succeed with images. The filtered runs acceptance test passed (3/3); formatting passed.
- GEN-04 is implemented; filtered generations keep `Filtered`/`IsFiltered` and never set `IsFailed`, while the second model succeeds. The filtered runs acceptance test passed (4/4); formatting passed.
- GEN-05 is implemented; a held fake provider response keeps the model card active, and releasing it completes the card with an image. The filtered runs acceptance test passed (5/5); formatting passed.
- GEN-06 is implemented; the scenario verifies the image file exists and is readable from storage before restart, then reopens the session and verifies the same card and file path are restored. The filtered runs acceptance test passed (6/6).
- GEN-07 is implemented; a `usage.cost` response is displayed as `$0.125` while the current culture is `fr-FR`. The filtered runs acceptance test passed (7/7).
- GEN-08 is implemented; with one completed and one held generation the run header shows `1 of 2 done`, then `2 of 2 done` after releasing the held response. The filtered runs acceptance test passed (8/8).
- GEN-09 is implemented; the generated 120×80 PNG displays `120×80 px · 3:2`. The filtered runs acceptance test passed (9/9).
- GEN-10 is implemented; changing selected models during a held run leaves its original two cards intact, and the next run uses only the newly selected model. The filtered runs acceptance test passed (10/10).
- GEN-11 is implemented; image catalog per-image prices are summed in the next run estimate. The filtered runs acceptance test passed (11/11).
- Full verification is incomplete: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj` hangs at the existing `Support/AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore`, including when run alone. The filtered `Category=runs` acceptance test passes (11/11), formatting passes, but the plan requires the full acceptance suite and the executor is not authorized to change `Support/AppWorldTests.cs`.
- Final verification on 2026-11-13 after task 277: the full acceptance suite passed (51 total, 50 passed, 1 skipped for SES-04 `@bug-276`); `Category=runs` passed (11/11); formatting and `bash scripts/check.sh` passed. The previous AppWorld restart hang no longer reproduces. GEN-01–GEN-11 are already marked `done` in `docs/specs/acceptance-scenarios.md`.

## Review
Reviewed 2026-11-17 by claude-planner. GEN-01–GEN-11 implemented; the earlier blocker was resolved by task 277 and the final full suite passed (2026-11-13). Catalog rows `done`. Not rerun locally (no .NET SDK in the planner container); relies on the Report and the merged PRs' CI.
Verdict: done.

## Comments

### 2026-11-09, claude-bdd-plan
Created at the owner’s request to plan application test scenarios for GPT Luna. Scenarios and expectations are in `docs/specs/acceptance-scenarios.md`. The plan is awaiting approval.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.

### 2026-11-11, codex-gpt6
GEN-01–GEN-11 are implemented and pass the filtered runs suite (11/11), and formatting passes. The full acceptance command hangs at `Support/AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore` (line 11), including when that test is run in isolation; the test synchronously disposes `AppWorld` inside the Avalonia UI dispatch callback (`using var world`), while `AppWorld.Dispose()` blocks on `DisposeAsync()`. The accepted `Files:` list does not include `Support/AppWorldTests.cs`, and the task requires the full acceptance suite to pass. Proposed plan change: add `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorldTests.cs` to `Files:` and make its async test disposal nonblocking (`await using`/await `DisposeAsync`), then rerun the full acceptance suite, formatting and `scripts/check.sh`. Task 257 is blocked pending approval of this scope change.

### 2026-11-13, claude-planner — plan amendment
Answers the mismatch of 2026-11-11: the hang of `AppWorldTests.RestartAsync_ReusesDataFolderAndSecretStore` was fixed by task 277 (done 2026-11-13, merged in PR #101), so no change of that file is needed here. GEN-01–GEN-11 are on `main`. Remaining work: rerun the full acceptance suite, formatting and `scripts/check.sh`, update the catalog statuses if not yet done, fill the Report and submit. The harness allowance of the other acceptance tasks applies here too.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
