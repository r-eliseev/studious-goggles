---
id: 287
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Preserve the stopped-waiting status and time for cancelled synchronous images

## Description

Task 258's approved CAN-01 amendment requires a cancelled synchronous image card to retain the "stopped waiting" notice; CAN-05 requires the cancellation time/status to remain visible after restart. Reproduction on 2026-11-17 in `ImageGen/ImageGen.Acceptance.Tests/Features/CancelAndRecovery.feature`: the fake OpenRouter image request is held, the user cancels the active card, and Core logs the generation as `Cancelled` with "Provider cancellation is unconfirmed; stopped waiting". The CAN-01 binding then waits 10 seconds for `GenerationItemViewModel.CancellationStatusText == AppResources.StoppedWaitingMedia` and times out.

Evidence: `ChatPageModel.CancelCardAsync` (`ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`) calls `GenerationItemViewModel.SetStoppedWaiting` after `GenerationService.CancelGenerationAsync` returns `StoppedWaiting`. A later `GenerationItemViewModel.Update` clears that status for a finished image because the special `_stoppedWaiting` retention branch only includes video/audio (`ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, around lines 498–506). Also, `GenerationService.PersistUserCancellationAsync` stores `Cancelled`/`CompletedAt` but does not set `Generation.CancelRequestedAt`; that timestamp is set only for a remote `CancellationRequested` outcome (around line 205). Thus an image's stopped-waiting status and cancellation time cannot currently be reconstructed from persisted state after an update or restart.

## Acceptance criteria

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, codex-20261117
Proposed plan: define and persist the timestamp/status for a submitted synchronous image request that the user stops waiting for; retain the stopped-waiting notice on its cancelled image card after data reload and app restart; add Core, Presentation and acceptance regression coverage for the status and timestamp. Check S3.5.1/S3.5.3 semantics and the existing `CancelRequestedAt` field before implementation; do not conflate a locally stopped request with a provider-confirmed cancellation. The planner should define the persisted meaning and exact UI text in the approved plan.
