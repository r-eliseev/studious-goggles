---
id: 280
status: approved
assignee:
claimed:
created: 2026-11-13
---

# Chat: warn before sending when the run is above the "expensive run" threshold

## Description

F8.4 says the configured “warn if a run costs more than X” threshold shows a warning before a chat message is sent. `SpendingService.CheckAsync` returns `SpendingWarning.ExpensiveRun` when the estimate reaches the configured threshold (`ImageGen/ImageGen.Presentation/Spending/SpendingService.cs`, `CheckAsync`), but `ChatPageModel.SendAsync` only displays an alert for `SpendingWarning.MonthlyLimitReached` (`ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `SendAsync`). Reproduction: set `ExpensiveRunThresholdUsd` below the selected models' next run estimate, then send a chat message; no warning is shown.

## Acceptance criteria
- `ChatPageModel.SendAsync`: when `SpendingService.CheckAsync` returns `SpendingWarning.ExpensiveRun`, show `dialogs.AlertAsync(AppResources.SpendingWarningTitle, AppResources.SpendingExpensiveRunMessage)` and then send the message as now (F8.4: a warning, not a prohibition). `MonthlyLimitReached` keeps its current alert. One alert per send.
- New resource `SpendingExpensiveRunMessage` = "This run's estimated known cost is above your \"expensive run\" threshold. You can still continue." (`AppResources.resx` and the accessor in `AppResources.cs`).
- Presentation test: threshold below the estimate → the alert with that text, then the message is sent; no threshold → no alert.
- Acceptance SPD-04: remove `@ignore @bug-280`; fix the step wording — the threshold is below the estimate (now it says "the next run estimate is below the configured spending threshold", which is the opposite); bind the steps if needed; green. Catalog row SPD-04 `done`.

## Analysis
- `ImageGen/ImageGen.Presentation/Spending/SpendingService.cs` `CheckAsync` (line ~39) already returns `ExpensiveRun`; `ChatPageModel.SendAsync` (`ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, line ~359) only handles `MonthlyLimitReached`.
- Existing tests for the monthly-limit alert in `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs` show how the estimator and spending service are faked.
- Acceptance: `ImageGen/ImageGen.Acceptance.Tests/Features/Spending.feature` (SPD-04, line ~73), `Steps/SpendingSteps.cs`.

## Plan
1. Presentation failing test (no alert is shown); resource and the branch in `SendAsync`; green.
2. SPD-04: wording, untag, bindings; green; catalog.

Files: `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Acceptance.Tests/Features/Spending.feature`, `ImageGen/ImageGen.Acceptance.Tests/Steps/SpendingSteps.cs`, `docs/specs/acceptance-scenarios.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
bash scripts/check.sh
```

## Report

## Review

## Comments

### 2026-11-13, codex
Proposed plan: add a user-visible warning for `SpendingWarning.ExpensiveRun` before sending the assistant message while still allowing it to be sent, as specified by F8.4. Add presentation coverage for the warning and add/update acceptance SPD-04 after the behavior is implemented. Files likely include `ChatPageModel.cs`, its presentation tests, and `Features/Spending.feature`/`Steps/SpendingSteps.cs`.

### 2026-11-14, claude-planner
Planned from the executor's proposal; the requirement is F8.4 ("for the assistant the warning is shown before the message is sent, without forbidding it").
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
