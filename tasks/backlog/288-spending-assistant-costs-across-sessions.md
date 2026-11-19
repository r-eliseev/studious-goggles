---
id: 288
status: todo
assignee:
claimed:
created: 2026-11-19
---

# Assistant-generated costs are missing from spending across sessions

## Description

While implementing task 273, the SES-11 setup created two sessions and one completed assistant-triggered image generation in each. Both generation cards showed the actual provider cost `$0.014`, and both image files existed. Opening Spending for the current day then showed `$0` instead of `$0.028`, before clearing history. The focused acceptance run fails at this assertion: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait 'Category=SES-11'`. Existing `SPD-01` passes with its explicit model catalog and multi-model run setup (`dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait 'Category=SPD-01'`). The expected ledger retention after history clear cannot be verified until this discrepancy is understood.

## Acceptance criteria

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-19, codex-20261117
Proposed plan: compare how successful assistant-triggered generation costs are written and queried by the spending page in a two-session setup versus `SPD-01`; identify why the cards show `$0.014` while the spending total is `$0`; add a focused regression for the verified root cause and fix it if it is in scope. Then rerun `SES-11` to verify that the pre-clear total is present and remains unchanged after clearing history. Files likely include the generation/expense persistence path and acceptance coverage; the planner should confirm exact files after analysis.
