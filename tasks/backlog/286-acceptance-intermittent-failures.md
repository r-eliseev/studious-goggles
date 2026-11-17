---
id: 286
status: approved
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

## Review

## Comments

### 2026-11-17, claude-planner
Created from the reviews of 261 and 263.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
