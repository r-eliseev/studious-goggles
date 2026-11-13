---
id: 271
status: blocked
assignee:
claimed:
created: 2026-11-09
---

# Translate the remaining Russian in the code to English

## Description
Part of 268. Identifiers and most comments are English already; Russian remains in 35 text files of `ImageGen/` (2026-11-09):
- exception messages: `ImageGen.Data/SqliteDatabase.cs`, `ImageGen.Data/Migrations.cs` (and the comment `NNNN_имя.sql`), `ImageGen.Avalonia/Services/Secrets/{SecretFileNames,KeychainSecretBackend,SecretServiceBackend}.cs`, `ImageGen.Avalonia/Services/Navigation/AvaloniaNavigationService.cs`, `ImageGen.Avalonia/Markup/ResExtension.cs`;
- a SQL comment: `ImageGen.Data/Migrations/0002_providers.sql`;
- tests: wait descriptions and test texts in `ImageGen.Avalonia.Tests/Flows` (10 files), `Views` (7), `Services/Secrets` (2), `TestDoubles` (1), `ImageGen.Core.Tests/Services` (2), `ImageGen.Data.Tests` (1), `ImageGen.Presentation.Tests/Design` (1);
- test data that checks Russian input on purpose: `ImageGen.Core.Tests/Search/FuzzyMatcherTests.cs` (fuzzy search over Russian words), possibly others.

Find the current list with `LC_ALL=C.UTF-8 grep -rlIP '[\x{0400}-\x{04FF}]' ImageGen --exclude-dir=obj --exclude-dir=bin`.

Depends on: —. If 267 is in progress, skip `ImageGen.Avalonia.Tests/Flows` (267 deletes these files) and list it in the "Report".
Environment: Linux or Windows.

## Acceptance criteria
- Exception and log messages are English. If a test asserts the text of a translated message, the test changes in the same commit; if a message is shown to the user, it must go through `AppResources` instead — do not change that here, create a task.
- `0002_providers.sql`: changing a comment of a released migration changes its bytes; check first whether migrations are checksummed (`ImageGen.Data/Migrations.cs`, `MigrationsTests`). If they are, leave the file and add it to `LATIN_EXCEPTIONS` with the reason "released migration, content is frozen (ADR 0002)".
- Test names stay `Method_Condition_Result` in English; wait descriptions and test strings that are not about non-English input are English.
- Test data that checks non-English input stays and goes to `LATIN_EXCEPTIONS` in `scripts/check-latin.sh` with the reason (for example "FuzzyMatcher must handle Cyrillic"). Do not move such data elsewhere.
- Binary assets (fonts, images) are skipped by the check; nothing to do.
- `ImageGen` is added to `LATIN_PATHS`; all test projects available on Linux are green.

## Plan
1. Production code: exception messages, project by project, with the tests that assert them.
2. Tests: project by project.
3. Exceptions list, `LATIN_PATHS`.

Files: the files listed in the "Description", `scripts/check-latin.sh`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/<Project>/<Project>.csproj --verify-no-changes   # each changed project
bash scripts/check.sh
```

### Execution order (for Luna)
- First read only the files of the "Files" line; work file by file and commit in small steps (one folder per commit).
- Translate without changing the meaning: do not shorten, do not "improve" the content, do not fix facts — a wrong fact becomes a comment or a new task, not a silent edit.
- Keep identifiers, paths, commands, ids, numbers, links and quoted UI texts unchanged; translate only the prose.
- If something is ambiguous, keep the original meaning and write a question in "Comments".
- After each commit run `bash scripts/check.sh`. In the "Report": what was translated, which paths were added to `LATIN_PATHS`, what stayed as an exception and why.

## Report

## Comments

### 2026-11-09, claude-bdd-plan
Part of 268. The plan waits for approval.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan)
Plan approved ("approve all tasks with plans").

### 2026-11-13, codex-gpt6
The approved plan has conflicting requirements for localized accessibility tests. `ImageGen/ImageGen.Avalonia.Tests/Views/ViewsAccessibilityTests.cs` asserts Russian accessible names (`чат`, `модели`, `ключ`, `сессии`, `настройки`), while the execution order says to keep quoted UI text unchanged. The acceptance criteria require adding `ImageGen/` to `LATIN_PATHS`, but `scripts/check-latin.sh` permits exceptions only for test data that verifies non-English input. Translating these assertions would make the tests disagree with the Russian resource values; exempting the file does not match the stated exception policy.

Proposed plan amendment: keep these localized accessible-name assertions unchanged and allow a narrowly documented `LATIN_EXCEPTIONS` entry for this test file with reason `assertions for Russian localized accessibility labels`; add `ImageGen/` to `LATIN_PATHS`, and keep only this file plus genuine non-English search input fixtures as exceptions. Alternatively, update the product localization and its plan in a separate approved task. No code or plan section was changed. Waiting for planner resolution.
