---
id: 284
status: approved
assignee:
claimed:
created: 2026-11-14
---

# Cancel of a submitted synchronous image request: stop waiting, mark Cancelled

## Description
Owner decision of 2026-11-14 (task 258): cancelling an OpenRouter image generation that is already sent must stop waiting and end as Cancelled, with a note that the provider may still charge — as for audio and video (ADR 0027). Today `GenerationService.CancelGenerationCoreAsync` (`ImageGen/ImageGen.Core/Services/GenerationService.cs`, the `remote is null` branch with `!requestedBeforeSubmit`, line ~148) returns `CancelFailed`, and `ChatPageModel` shows "could not cancel" while the card keeps waiting. Synchronous image providers (OpenRouter images, OpenAI-compatible) run through `InMemoryQueuedImageProvider` over `ISynchronousImageTransport`; there is no remote job to cancel.

Depends on: —. Task 258 waits for this one.
Environment: Linux or Windows.

## Acceptance criteria
- An active image generation of a synchronous provider, after submission, on user cancel: the HTTP request is cancelled (the job's `Source`), the generation is stored as `Cancelled` (`PersistUserCancellation`, the same path as audio, line ~99–114), the result is `GenerationCancelResult.StoppedWaiting`, and a late response is not saved (S3.5.2).
- The card shows the existing "stopped waiting" status (`GenerationItemViewModel.SetStoppedWaiting`, resource `StoppedWaitingMedia`); check that its text fits images too ("the provider may still finish and charge"); if it names video/audio only, generalize the text.
- Queued providers (fal) and cancellation before submission keep their current behavior; their existing tests stay green.
- Run cancellation (`CancelRunAsync`) uses the same path.
- `docs/specs/chat-sessions.md` S3.5.1: one sentence — for a provider without a remote job, cancelling a submitted request stops waiting and stores "cancelled"; the provider may still charge.
- Tests: Core — cancel a held synchronous request after submission → `StoppedWaiting`, status `Cancelled`, the late image not saved; cancel before submission unchanged; fal path unchanged. Presentation — the card shows the stopped-waiting status.

## Analysis
- How the service knows the provider is synchronous: find it in the code first (the generation's provider resolved through `IProviderRouter` being `InMemoryQueuedImageProvider`, or a flag on the running job). Prefer an explicit property on the job set where the provider is chosen over a type check in the cancel path. If neither fits without changing provider interfaces, stop and ask in "Comments".
- Existing cancel tests: `ImageGen/ImageGen.Core.Tests/Services/GenerationService*Tests.cs` (find the cancel ones with `grep -ln "CancelGenerationAsync" ImageGen/ImageGen.Core.Tests`); Presentation: `ChatPageModelTests` around `StoppedWaiting`.

## Plan
1. Core failing test (fails: result is `CancelFailed`); the change; green. Then the late-response and run-cancel tests.
2. Presentation test and resource text if needed.
3. Spec sentence.

Files: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, the Core cancel test file(s) found in Analysis, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs` (only if needed), `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `docs/specs/chat-sessions.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
bash scripts/check.sh
```

## Report

## Review

## Comments

### 2026-11-14, owner (in the chat, recorded by claude-planner)
Asked what Cancel should do for a submitted OpenRouter image request without a job id (task 258's blocker): "Stop waiting → Cancelled".
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
