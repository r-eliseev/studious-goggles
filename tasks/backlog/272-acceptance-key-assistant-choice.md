---
id: 272
status: todo
assignee:
claimed:
created: 2026-11-10
---

# Acceptance first-run assistant model choice

## Описание
The acceptance catalog says that a valid OpenRouter key on first run opens assistant model selection before the session list. The current `ApiKeyPageModel.SaveAsync` only opens that chooser when the connected provider differs from the existing default assistant provider. Since the default is OpenRouter, the scenario currently proceeds straight to the session list.

## Критерии готовности
- Owner decision recorded on whether the catalog or application behavior is intended.
- If behavior changes, add a regression acceptance scenario for a valid first-run key and verify the selected assistant model is saved.
- If the catalog is corrected, update KEY-02 expectation and coverage to match the approved behavior.

## Анализ
Evidence: `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs` checks `providerId != defaultAssistantModel.Current.ProviderId` before showing the chooser; `ImageGen/ImageGen.Core/Domain/Sessions/SessionSettings.cs` defaults the provider to OpenRouter; `docs/specs/acceptance-scenarios.md` currently expects the chooser for KEY-02.

## План
Ask owner to decide whether every first-run key should be followed by assistant model selection or whether OpenRouter's default should skip the choice. Implement only after the decision; update the scenario and catalog, then run Acceptance tests and formatting.

Files: `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs` only if behavior changes; `ImageGen/ImageGen.Acceptance.Tests/Features/FirstRunAndKeys.feature`; `docs/specs/acceptance-scenarios.md`.

## Отчёт

## Комментарии
### 2026-11-10, codex-gpt6
Observed while implementing task 254. The existing product code and acceptance catalog disagree; no product behavior or requirement was changed. Awaiting owner decision.
