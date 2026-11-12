---
id: 272
status: review
assignee:
claimed:
created: 2026-11-10
---

# Acceptance first-run assistant model choice

## Description
The acceptance catalog says that a valid OpenRouter key on first run opens assistant model selection before the session list. The current `ApiKeyPageModel.SaveAsync` only opens that chooser when the connected provider differs from the existing default assistant provider. Since the default is OpenRouter, the scenario currently proceeds straight to the session list.

Depends on: 254.
Environment: Linux or Windows (headless).

## Acceptance criteria
- Owner decision recorded on whether the catalog or application behavior is intended.
- If behavior changes, add a regression acceptance scenario for a valid first-run key and verify the selected assistant model is saved.
- If the catalog is corrected, update KEY-02 expectation and coverage to match the approved behavior.

## Analysis
Evidence: `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs` checks `providerId != defaultAssistantModel.Current.ProviderId` before showing the chooser; `ImageGen/ImageGen.Core/Domain/Sessions/SessionSettings.cs` defaults the provider to OpenRouter; `docs/specs/acceptance-scenarios.md` currently expects the chooser for KEY-02.

The specification agrees with the code: `docs/specs/chat-sessions.md`, S6.4 — "First run requires the key of the provider of the chosen assistant model; on the key screen you can choose another chat provider and then a model from its catalog". The chooser is part of switching the chat provider, not of every first run. The catalog row KEY-02 (written in the planning session of 2026-11-09) misread this. Decision (owner, 2026-11-10, recorded in "Comments"): correct the catalog, keep the code.

## Plan
1. KEY-02 becomes: "A valid key of the default assistant provider (OpenRouter) → status "works", the session list opens without the model choice; the default assistant model stays `defaults.json`'s value". Update the catalog row (requirements `F1.2, S1.3, S6.4`, status `done` once green) and the scenario in `Features/FirstRunAndKeys.feature`; remove `@ignore @bug-272`.
2. A new scenario KEY-12 for the other branch of S6.4: on the key screen the user chooses another configured chat provider (a custom OpenAI-compatible provider with `chatModels` from a test `providers.json` in the world's data folder — check that `AppWorld` can place it; if it cannot, write a question in "Comments" instead of extending the world beyond a file write), saves its key → the model choice of that provider's catalog opens; choosing a model saves it as the default for new sessions (check `defaults.json` through the Settings screen, not by reading the file).
3. Both scenarios: first the `.feature` change, the run fails for the described reason, then the bindings.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/FirstRunAndKeys.feature`, `ImageGen/ImageGen.Acceptance.Tests/Steps/FirstRunSteps.cs`, `ImageGen/ImageGen.Acceptance.Tests/Steps/CommonSteps.cs`, `ImageGen/ImageGen.Acceptance.Tests/Support/AppWorld.cs` (only a helper that writes `providers.json`, if needed), `docs/specs/acceptance-scenarios.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Execution order (for Luna)
- Do not change `ApiKeyPageModel` or any product code: the code matches the specification.
- Steps strictly in order (ub-mode): the scenario first, it fails for the described reason, then the minimal binding.
- If the code differs from the plan, stop and write a question in "Comments".
- Write everything in English; in the "Report": scenarios, commands and results.

## Report

- Corrected KEY-02 to verify that a valid OpenRouter key opens Sessions without the assistant model chooser and preserves the default model.
- Added KEY-12 with a temporary OpenAI-compatible chat provider in the acceptance world's `providers.json`; selected it on first run, checked that its assistant model chooser appears, saved the model, and verified it in Settings.
- Updated `docs/specs/acceptance-scenarios.md`; both KEY-02 and KEY-12 are marked `done`.
- Verification: `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj` passed (42 passed, 1 skipped); `dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes` and `bash scripts/check.sh` passed.

## Comments
### 2026-11-10, codex-gpt6
Observed while implementing task 254. The existing product code and acceptance catalog disagree; no product behavior or requirement was changed. Awaiting owner decision.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan)
The specification (S6.4) matches the code; the catalog row KEY-02 was wrong. Correct the catalog and the scenario, add KEY-12 for choosing another chat provider. Plan approved.
