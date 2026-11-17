---
id: 286
status: review
assignee:
claimed:
created: 2026-11-17
---

# Acceptance suite: find the intermittent failures (diagnosis)

## Description
Three executors reported intermittent failures of the full acceptance suite (`ImageGen.Acceptance.Tests`) that passed on a retry:
- task 261 Report: "The first full-suite attempt timed out in SES-02; its retry passed";
- task 263 Report: "Two earlier full-suite attempts each encountered one unrelated intermittent failure in existing scenarios; the third full run passed";
- task 255 history: SES-02/SES-05 earlier did not complete within the headless scenario timeout (2026-11-10, later fixed by advancing test time in teardown).
The failing scenario names and errors of the 263 runs were not recorded. A flaky suite will turn `main` red at random and hide real regressions.

Depends on: —.
Environment: Linux (headless); GitHub Actions for repeated runs if a local loop is too slow.

## Acceptance criteria
- At least 20 full runs of the acceptance suite on Linux (a local loop, or a temporary `workflow_dispatch` job on the task branch that loops the test command, reverted before the PR), with every failure's scenario name, step, error message and duration recorded in "Comments" (a table: run, scenario, step, error).
- For each failing scenario: the hypothesis with evidence (for example a fixed wait in `AppWorld.WaitUntilAsync` that real time can exceed, a background task racing teardown, a shared static between worlds).
- Finish as `review` with the findings and a proposed fix per cause (files and steps), or "no failure in 20 runs" with the run links. Do not change tests or code to make them pass in this task, except temporary instrumentation that is reverted.

## Plan
1. Loop the suite 20 times, saving the TRX/console of each run under `tmp/` (not committed) or as workflow artifacts.
2. Summarize failures; read the related steps and `Support/AppWorld.cs` waits; write hypotheses.
3. Report and proposed fixes.

Files: none committed except this task card (temporary workflow edit allowed only on the task branch and reverted).

### Verification
```
for i in $(seq 1 20); do dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj || echo "run $i failed"; done
bash scripts/check.sh
```

## Report

Ran the complete 87-case suite 20 times with the xUnit v3 in-process runner; 4 runs passed (2 skipped each) and 16 runs failed (24 scenario failures total). No test or product code was changed. `dotnet build ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --no-restore` succeeded. On this Linux SDK 10.0.403 host, `dotnet test --project ...` often stopped before discovery with exit 5 (`Zero tests ran`, `Handshake failures`), so the completed 20-run series used the test assembly's bundled xUnit runner directly. A diagnostic `dotnet test --project ... --verbosity diag` invocation did start the suite: 82 passed, 3 failed, 2 skipped in 30.480 seconds.

To separate pre-existing failures from the current branch, built and ran the same suite at pre-236 commit `084c2fe` in a temporary worktree: 4 of 5 runs passed; run 2 failed the monthly-limit persistence and attached-picture scenarios. This confirms those two failure types predate task 236. The current branch also showed Avalonia thread-affinity errors, stale/incorrect UI values and action lookup failures. The detailed run matrix and hypotheses are in Comments.

`bash scripts/check.sh` — passed. `git diff --check` — passed. Temporary console logs and the comparison worktree are under `tmp/task-286/` and are not tracked.

## Review

## Comments

### 2026-11-17, codex-20261117 — 20-run Linux diagnosis

Run command: `dotnet ImageGen/ImageGen.Acceptance.Tests/bin/Debug/net10.0/ImageGen.Acceptance.Tests.dll -noLogo -noColor` (xUnit v3 in-process runner). Each run discovered 87 cases; 2 cancellation scenarios were ignored. Suite duration is recorded below for each run; the reported failing-step duration was 0.0s for assertion failures and 30.0–30.2s for chat timeouts.

| Run (suite duration) | Scenario / failed step | Error |
|---|---|---|
| 1 (31.460s) | Actions menu reflects failed generation / opening menu | `Assert.Single`: no matching action |
| 1 (31.460s) | Attached picture / checking image request | Avalonia Dispatcher thread-affinity exception |
| 1 (31.460s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |
| 4 (31.274s) | Session assistant model visible / checking chat label | Avalonia Dispatcher thread-affinity exception |
| 5 (36.423s) | Repeating completed generation / sending first message | `Assert.Contains`: user message absent |
| 6 (33.024s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |
| 7 (32.168s) | Session assistant model visible / checking chat label | Avalonia Dispatcher thread-affinity exception |
| 9 (62.998s) | Attached picture / submitting message | Timed out after 30.0s; `Chat.IsBusy=true`, catalog GETs recorded, chat POST absent |
| 9 (62.998s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |
| 10 (28.916s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |
| 11 (30.498s) | Next run estimate / checking estimate | Expected `$0.05`, actual `$0.02` |
| 11 (30.498s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |
| 12 (31.033s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |
| 13 (65.447s) | Session assistant model visible / checking chat label | Avalonia Dispatcher thread-affinity exception |
| 13 (65.447s) | Attached picture / submitting message | Timed out after 30.2s; `Chat.IsBusy=true`, catalog GETs recorded, chat POST absent |
| 13 (65.447s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |
| 14 (35.684s) | Actions menu reflects cancelled generation / opening menu | `Assert.Single`: no matching action |
| 15 (62.251s) | Actions menu reflects filtered generation / opening menu | `Assert.Single`: no matching action |
| 15 (62.251s) | Attached picture / submitting message | Timed out after 30.2s; `Chat.IsBusy=true`, catalog GETs recorded, chat POST absent |
| 15 (62.251s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |
| 16 (31.454s) | Semantic search setup / sending message | `Assert.Contains`: user message absent |
| 17 (33.230s) | Sunday expense / sending message | `Assert.Contains`: user message absent |
| 18 (32.314s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |
| 19 (32.189s) | Monthly limit persists / checking limit after restart | Expected `42.50`, actual empty |

Run outcomes: runs 2, 3, 8 and 20 passed; runs 1, 4–7, 9–19 failed. Two ignored cancellation cases in each run are tracked by task 287.

### Hypotheses and proposed fixes

1. **Shared headless Avalonia dispatcher and off-thread UI access.** `HeadlessHooks` starts one static `HeadlessApp` session for the assembly, `AppWorld` creates a new window per scenario, and the acceptance project has no xUnit parallelization attribute/collection. `AppWorld.WaitUntilCoreAsync` directly calls `Dispatcher.UIThread.RunJobs`; `CommonSteps.ThenTheRequestToOpenRouterHasAValueAtJsonPath` and `SessionsSteps.ThenTheChatShowsTheDefaultAssistantModel` call it outside `HeadlessApp.RunAsync`. The thread-affinity failures point directly at this ownership mismatch; action lookup and generation/send races may share it. Proposed follow-up: serialize scenarios sharing `HeadlessApp` (acceptance test assembly configuration / a single xUnit collection), and marshal `RunJobs` plus all UI-bound reads through `HeadlessApp.RunAsync` (`ImageGen.Acceptance.Tests/Support/HeadlessApp.cs`, `Support/AppWorld.cs`, `Steps/CommonSteps.cs`, `Steps/SessionsSteps.cs`). Verify targeted failures with parallelism enabled and disabled, then repeat the full suite.

2. **Chat reload can publish stale state after asynchronous catalog reads.** In current `ChatPageModel.ReloadAsync`, the epoch is checked after loading the timeline, then the method awaits unavailable-model catalog calls before clearing/rebuilding `Items` and updating the estimate. It does not recheck the epoch after those awaits. This is consistent with messages disappearing after send and run estimate reverting to the old one-model `$0.02` after choosing two models (`$0.05`). Proposed follow-up: keep timeline rendering independent from catalog network calls, and recheck the reload/session epoch before every UI update after awaited catalog work; add delayed-catalog tests for concurrent reload, send, and model-selection navigation (`ImageGen.Presentation/PageModels/ChatPageModel.cs` and `ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, with acceptance coverage in `ImageGen.Acceptance.Tests/`). The attached-message timeout also occurred before #236 (see baseline below), so catalog awaits are a possible aggravating factor, not proven as its original cause.

3. **Spending screen load is fire-and-forget in the test path.** `SpendingView.OnAppearing` starts `LoadCommand` with `FireAndForgetSafeAsync`; `SpendingSteps.OpenSpendingAsync` waits for the route and refreshes totals but does not wait for `MonthlyLimitText` to load. The following synchronous assertion can run while the property is still empty. Proposed follow-up: give the headless navigation/test path an awaitable load-completion signal or wait for the persisted value before asserting (`ImageGen.Avalonia/Views/SpendingView.axaml.cs`, `ImageGen.Acceptance.Tests/Steps/SpendingSteps.cs`).

Baseline comparison at commit `084c2fe` (before task 236): five direct-run suites produced four passes and one failure; run 2 failed the same monthly-limit assertion and attached-picture send timeout (30.2s, `Chat.IsBusy=true`, no chat POST). This establishes those two as pre-existing. The current branch additionally showed thread-affinity errors, stale estimate and missing user-message/action failures; the current `ChatPageModel.ReloadAsync` missing a post-catalog epoch check is a concrete follow-up hypothesis, not a proven root cause for every failure.

`dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj` also intermittently failed before test discovery with exit 5 (`Zero tests ran`, `Handshake failures`) under SDK 10.0.403. A successful diagnostic invocation completed 87 cases (82 passed, 3 failed, 2 skipped; 30.480s). Keep this launcher issue separate from scenario failures.

### 2026-11-17, claude-planner
Created from the reviews of 261 and 263.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
