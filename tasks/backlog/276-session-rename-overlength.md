---
id: 276
status: todo
assignee:
claimed:
created: 2026-11-11
---

# Reject overlength session rename titles

## Описание

The accepted sessions specification says that renaming to more than 40 characters is not accepted (`docs/specs/acceptance-scenarios.md`, SES-04). The current behavior silently truncates the title to 40 characters: `ImageGen/ImageGen.Core/Services/SessionService.cs`, `RenameAsync` trims the input and takes `trimmed[..Session.MaxTitleLength]`. The UI then updates the row to that truncated title in `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `RenameSessionAsync`.

Reproduction: in acceptance scenario `ImageGen/ImageGen.Acceptance.Tests/Features/Sessions.feature`, queue the rename prompt result `This title is deliberately longer than forty characters` and rename a new session. Expected: the title remains `New session`; actual: it becomes `This title is deliberately longer than f` (40 characters). The empty-title case is rejected and displays an alert.

Depends on: 255.
Environment: Linux or Windows.

## Критерии готовности

## Анализ

## План

## Отчёт

## Review

## Комментарии

### 2026-11-11, codex-gpt6
Proposed plan:
- Reject rename input longer than `Session.MaxTitleLength` instead of silently truncating it; keep the existing title unchanged and show the same kind of user-facing validation as the empty-title case.
- Add a `SessionService` regression test for a 41-character title and verify valid renames remain unchanged.
- Once task 255 is merged, remove `@ignore @bug-276` from the SES-04 long-title scenario and update the SES-04 catalog status to `done`.
- Candidate files: `ImageGen/ImageGen.Core/Services/SessionService.cs`, its Core test, `ImageGen/ImageGen.Acceptance.Tests/Features/Sessions.feature`, and `docs/specs/acceptance-scenarios.md`.
