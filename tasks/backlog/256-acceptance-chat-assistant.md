---
id: 256
status: done
assignee:
claimed:
created: 2026-11-09
---

# Acceptance scenarios: chat and assistant

## Description
Automate the “Chat and assistant” section of the catalog: CHAT-01 … CHAT-08. File: `Features/ChatAssistant.feature`, tag: `@chat`.

Depends on: 253.
Environment: Linux or Windows (headless).

## Acceptance criteria
- CHAT-01 … CHAT-08 are in `.feature`; each passes or is tagged `@ignore @bug-NNN` with a task.
- CHAT-02 (F9.4, no hidden substitution): the prompt from `generate_images` arguments exactly matches the prompt in the run block, on the card, and in the generation request body (fake request log). The request model is the session model.
- CHAT-03: the second chat-completion request in a session contains the system prompt (`AssistantPrompts`) and the latest run prompt. Check for their presence, not the full prompt text.
- CHAT-04 — a `Scenario Outline` with examples 500, 429, and 401: the feed contains a separate error message with its cause (text from `ErrorKindText`), and the user message remains in the feed.
- CHAT-05: an empty or whitespace-only input disables the send command (`SendCommand.CanExecute` or its code equivalent).
- CHAT-08: attach an image through the `IFilePickerService` fake (a PNG test asset); the generation request body contains a reference. If F10.6 consent requires a dialog, answer “yes” through the dialog fake. The reference field shape follows the OpenRouter client code.
- Update the CHAT-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs` (`SendAsync`, `Items`, `AttachImageAsync`, `PendingAttachments`), `MessageItemViewModel.cs`, and `RunItemViewModel.cs` (`PromptText`).
- Core: `ImageGen/ImageGen.Core/Services/AssistantService.cs` (after `generate_images`, the turn ends without a second assistant response — clarified in task 243), `AssistantPrompts.cs`, and `AttachmentService.cs`.
- Flow tests: `Flows/ChatFlowTests.cs`, `Flows/AssistantRetryFlowTests.cs`, and `Flows/ChatKeyboardFlowTests.cs`.
- Specifications: `docs/specs/chat-sessions.md` S2.1–S2.5; `docs/specs/image-client.md` F4.5, F9.4, F10.6.

## Plan
Implement one scenario at a time, CHAT-01 → CHAT-08. Use one `Scenario Outline` for CHAT-04. Check request bodies through a `Then the <kind> request to OpenRouter contains …` step in `CommonSteps` (search by JSON path, not substring).

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/ChatAssistant.feature`, new `Steps/ChatSteps.cs`, `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs` (new responses only), a new PNG asset in `ImageGen/ImageGen.Acceptance.Tests/Assets/`, `docs/specs/acceptance-scenarios.md`, and `Support/FakeOpenRouter.cs` plus `Support/AppWorld.cs` (additive test-harness changes only: configurable fake responses, catalog fields, and world helpers; no behavior changes used by other features and no product code).

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=chat"
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

- CHAT-01 is implemented in `Features/ChatAssistant.feature`; the step bindings assert the assistant text appears in the feed and no run item is present.
- Verification: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=chat"` passed (1/1); `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes` passed.
- CHAT-02 is implemented; the scenario checks exact prompt equality in the run, generation card, and `/api/v1/images` request, and checks that the request model matches the card model. The filtered chat acceptance run passed (2/2); formatting passed.
- CHAT-03 is implemented; the second chat request is checked for both `AssistantPrompts.System` and the latest run's `CurrentPrompt` system message. The filtered chat acceptance run passed (3/3); formatting passed.
- CHAT-04 is implemented as an outline for HTTP 500, 429, and 401. Each example verifies the user message remains and an error message matches its `ErrorKindText`. The filtered chat acceptance run passed (6/6); formatting passed.
- CHAT-05 is implemented; whitespace-only input disables `SendCommand`. The filtered chat acceptance run passed (7/7); formatting passed.
- CHAT-06 is implemented; after an assistant error, retrying its message consumes the queued tool response and starts a run with the exact prompt. The filtered chat acceptance run passed (8/8); formatting passed.
- CHAT-07 is implemented and reuses the existing model-label assertion also used by SES-06. The filtered chat acceptance run passed (9/9); formatting passed.
- CHAT-08 is implemented. The scenario uses a reference-capable fake image model, the test `IFilePickerService` queues `Assets/reference.png`, and the recorded OpenRouter generation request is checked for its first `input_references` entry. The fake catalog gained an optional maximum reference count, and `CommonSteps` gained JSON path assertions for array indices.
- Verification: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=chat"` passed (10/10); the full acceptance suite passed (51 passed, 1 skipped); `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes` passed; `bash scripts/check.sh` passed.
- The chat-local submission step allows 30 seconds for command completion and checks Avalonia-bound state on the UI dispatcher; this keeps the session-specific timeout behavior localized to `ChatAssistant.feature`.

## Review
Reviewed 2026-11-17 by claude-planner. CHAT-01–CHAT-08 implemented; CHAT-08 uses a reference-capable fake model (`input_references` maximum, amendment of 2026-11-13) and checks `input_references[0]` in the request. Full acceptance suite and formatting passed per the Report. Not rerun locally (no .NET SDK in the planner container); relies on the Report and the merged PRs' CI.
Verdict: done.

## Comments

### 2026-11-09, claude-bdd-plan
Created at the owner's request to plan application test scenarios for GPT Luna. Scenarios and expectations are in `docs/specs/acceptance-scenarios.md`. The plan is awaiting approval.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. The changes from that day apply: Gherkin scenarios, step texts, bindings, comments, and the “Report” are in English (`docs/conventions.md`, “Language”; ADR 0031 is accepted); a “Structure of a scenario” in this plan means a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). Task 267 deletes the flow tests in `ImageGen.Avalonia.Tests/Flows` covered by this task.

### 2026-11-11, codex-gpt6
Plan mismatch for CHAT-08: `ReferenceImageLimit.Of` only permits a generation to send references when the selected catalog model declares an `input_references` range. `ImageGen/ImageGen.Acceptance.Tests/Support/FakeOpenRouter.cs` currently serializes only `seed` in fake image-model parameters, so the configured models cannot take references. The approved plan permits changes to this fake only for additional assistant responses. Proposed plan change: allow a configurable `input_references` maximum in the fake image-model catalog response, and configure a reference-capable model in CHAT-08 before attaching an image. Keep product code unchanged. The task is blocked until this file-scope change is approved.

### 2026-11-13, claude-planner — plan amendment
Answers the CHAT-08 mismatch of 2026-11-11. Verified: `ReferenceImageLimit.Of` needs the catalog's `input_references` range, and the fake image catalog serializes only `seed`. The `Files:` line now allows additive harness changes in `Support/FakeOpenRouter.cs` (a configurable `input_references` maximum per fake image model; configure a reference-capable model in CHAT-08 before attaching) and `Support/AppWorld.cs`. Product code stays unchanged. Note: `Features/ChatAssistant.feature` and `Steps/ChatSteps.cs` already exist on `main` (PR #101) — continue from them.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
