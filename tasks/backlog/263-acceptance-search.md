---
id: 263
status: approved
assignee:
claimed:
created: 2026-11-09
---

# Acceptance scenarios: session search (Words and Meaning)

## Description
Automate the “Поиск” section of the catalog: SRCH-01 … SRCH-04. Add SRCH-05 after tasks 226–228. File `Features/Search.feature`, tag `@search`.

Depends on: 253, 257 (run steps). If task 217 (embedding model name) is not merged when this task is claimed, compare the fake's embeddings route with `main`.
Environment: Linux or Windows (headless).

## Acceptance criteria
- Add SRCH-01 … SRCH-04 to `.feature`; each scenario passes or has `@ignore @bug-NNN` and a tracking task.
- SRCH-01/02: `SessionsPageModel.SearchText`, `SearchResults`, `HasNoResults`, `ClearSearch`; search by session title and generation prompt.
- SRCH-03: the fake responds to `POST embeddings` (`OpenRouterClient`, around line 559) with vectors from a table in the step: each text maps to a vector, with similarity determined by the scenario. Use the dimensionality expected by the code (check the code; the actual dimensionality of `baai/bge-m3` is unknown, see `docs/status.md`). Enable semantic search with `SettingsPageModel.ToggleSemanticSearchAsync`; wait for indexing to finish using `SemanticIndexStatus`.
- SRCH-04: `SelectSemanticSearch` / `SelectWordSearch` keep results from the two modes separate.
- Update SRCH-* statuses in the catalog.

## Analysis
- `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs` (`SearchText`, `SearchResults`, `IsSemanticSearchMode`, `SemanticIndexStatus`), `SettingsPageModel.cs` (`ToggleSemanticSearchAsync`, `StopSemanticIndexAsync`).
- Core: `ImageGen/ImageGen.Core/Search/SessionSearchService.cs`, `ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`, `SemanticSearchService.cs`.
- Specification: `docs/specs/chat-sessions.md` S1.4; ADR 0016.

## Plan
Implement scenarios SRCH-01 → SRCH-04 one at a time.

Files: `ImageGen/ImageGen.Acceptance.Tests/Features/Search.feature`, `Steps/SearchSteps.cs` (new), `Steps/CommonSteps.cs`, `Support/FakeOpenRouter.cs` (embeddings route), `docs/specs/acceptance-scenarios.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=search"
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
(The tag filter applies if Reqnroll passes tags as xUnit categories; task 252 records how this works in `docs/knowledge/acceptance-tests.md`.)

### Execution order (for Luna)
- First read only the files in the “Files” line, the named tests, and sections of `docs/specs/acceptance-scenarios.md`; compare signatures with current `main`.
- Follow steps in order (ub-mode): add a scenario to `.feature`, run it and confirm it fails because the step is unbound or the expectation is unmet; add the minimal binding; get a passing scenario. One scenario per commit (include the scenario id in the commit message).
- Derive scenario expectations from the specification, not the code. If the code behaves differently, do not weaken the scenario or fix code: add `@ignore @bug-NNN`, create a task with reproduction steps using `scripts/new-task.sh --publish backlog <name>`, and set the catalog status to “расхождение (NNN)”.
- Steps act as a user: use page model commands, select items, and navigate. Do not call repositories, Core services, or SQL directly, except in “Given” steps that prepare the world and in “file exists” checks.
- Add shared steps to `Steps/CommonSteps.cs`; first check whether a step already exists with different wording.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature), do not improvise; stop and write a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the “Report”, list passing scenarios, ignored scenarios and their tracking tasks, commands and results, and differences from the plan. Update scenario statuses in `docs/specs/acceptance-scenarios.md`.

## Report

## Comments

### 2026-11-09, claude-bdd-plan
Created at the owner's request to plan application test scenarios for GPT Luna. Scenarios and expectations are in `docs/specs/acceptance-scenarios.md`. The plan awaits approval.

### 2026-11-09, owner (in the chat, recorded by claude-bdd-plan)
Plan approved, status `approved`. Changes of the same day apply: the Gherkin scenarios, step texts, bindings, comments and the "Report" are in English (`docs/conventions.md`, "Language"; ADR 0031 is accepted); a "Structure of a scenario" in this plan is a `Scenario Outline`. Scenario expectations and wording are in the English catalog `docs/specs/acceptance-scenarios.md`. The acceptance project runs in CI (task 252). The flow tests in `ImageGen.Avalonia.Tests/Flows` that a scenario of this task covers are deleted by task 267, not here.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan) — plan extended
Tasks 226–228 are done, so SRCH-05 is added to this task: the index status in Settings (`SettingsPageModel.SemanticIndexStatus`) goes Off → Indexing → Ready while the embeddings fake answers; choosing another embedding model (`EmbeddingModels`, `SelectedEmbeddingModel`, `ChangeEmbeddingModelAsync`, the catalog from the OpenRouter fake) reindexes the history (requests to `embeddings` with the new model id in the fake's log); an unavailable model is shown (`IsEmbeddingModelUnavailable`). Split SRCH-05 into separate catalog rows if one scenario gets too long (next free SRCH ids). Files: the same as in the plan plus `Support/FakeOpenRouter.cs` (the embedding model catalog route). Plan approved.
