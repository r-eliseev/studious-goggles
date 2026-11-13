---
id: 280
status: todo
assignee:
claimed:
created: 2026-11-13
---

# expensive-run-threshold-warning

## Description

F8.4 says the configured “warn if a run costs more than X” threshold shows a warning before a chat message is sent. `SpendingService.CheckAsync` returns `SpendingWarning.ExpensiveRun` when the estimate reaches the configured threshold (`ImageGen/ImageGen.Presentation/Spending/SpendingService.cs`, `CheckAsync`), but `ChatPageModel.SendAsync` only displays an alert for `SpendingWarning.MonthlyLimitReached` (`ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `SendAsync`). Reproduction: set `ExpensiveRunThresholdUsd` below the selected models' next run estimate, then send a chat message; no warning is shown.

## Acceptance criteria

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-13, codex
Proposed plan: add a user-visible warning for `SpendingWarning.ExpensiveRun` before sending the assistant message while still allowing it to be sent, as specified by F8.4. Add presentation coverage for the warning and add/update acceptance SPD-04 after the behavior is implemented. Files likely include `ChatPageModel.cs`, its presentation tests, and `Features/Spending.feature`/`Steps/SpendingSteps.cs`.
