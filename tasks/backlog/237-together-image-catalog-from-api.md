---
id: 237
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-13T07:37:07Z
created: 2026-11-07
---

# Together AI: load image model list from the API instead of the built-in file

## Description
Owner's question on 2026-11-07 about hard-coded lists: Together AI image models are five FLUX entries in `ImageGen/ImageGen.Core/Providers/builtin-providers.json` (`imageModels`), returned by `OpenAiCompatibleClient.ListImageModelsAsync` from config. Load the list from Together's API, as for OpenRouter and fal.

The response format for Together's model-list API was not found in the documentation (https://docs.together.ai, 2026-11-07): the docs include catalog tables and the SDK has `client.models.list()`, but the response fields are not described. Therefore step 1 is a live request using the owner's Together key; without the key, the task cannot proceed beyond step 1.

Depends on: —
Environment: Linux or Windows; live request requires the owner's Together AI key (free: model listing).

## Acceptance criteria
- Step 1 (facts): make an authenticated `GET https://api.together.xyz/v1/models` (or `https://api.together.ai/v1/models`, as set by `baseAddress` in `builtin-providers.json`) and record in `docs/knowledge/` (a new Together AI section in `docs/knowledge/media-generation.md` or a nearby file): response shape (array or object), model fields (`id`, model type such as `type`, name, price and its unit), and which models are images. Without a key, stop and note in "Comments" that the key is required.
- If the response identifies image models: `OpenAiCompatibleClient.ListImageModelsAsync` loads image models from the API for a provider with the new config flag `imageModelsFromApi: true` (Together), including name, price if available, and `SupportsSeed` (as for other providers with no explicit signal: `false` if the API does not provide it). Keep Together's `imageModels` config as a fallback if the API request fails (with a Warning). Do not change other OpenAI-compatible providers in user `providers.json`.
- If the response does not identify image models, keep the config list, record the fact, and close the task with that conclusion.
- Tests use a fixture based on the real response (trim it to 3–5 models; do not save the key or personal data): image models are selected, others are not; API error → config list and Warning.

## Analysis
`ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs` (`ListImageModelsAsync` from `Config.ImageModels`), `ImageGen/ImageGen.Core/Providers/builtin-providers.json`, provider config type (`ProviderConfig`, validated in `ProviderRegistry` — the new field must pass validation and be added to the config spec in `docs/specs/image-client.md` if it describes the `providers.json` format).

## Plan
1. Make a live request and record the facts (step 1).
2. Based on the result, write failing fixture tests, add the config flag, and load from the API with a fallback list; or close with the conclusion.
3. Document the config.

Files: `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core/Providers/builtin-providers.json`, `ImageGen/ImageGen.Core/Providers/ProviderConfig.cs` (or wherever the config is defined; `rg -n "class ProviderConfig" ImageGen`), `ImageGen/ImageGen.Core/Providers/ProviderRegistry.cs`, tests in `ImageGen/ImageGen.Core.Tests/`, `docs/knowledge/media-generation.md`, and, if needed, `docs/specs/image-client.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
bash scripts/check.sh
```

### Execution order (for Luna)
- First read only the files in the "Files" line and the named tests; compare signatures with current `main`.
- Follow the steps in order (ub-mode): failing test → run it and confirm it fails for the described reason → minimal fix → green test.
- Do not change files outside the "Files" line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature, test already exists), do not improvise; stop and write a question in "Comments".
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the "Report", record what changed, commands and results, and differences from the plan.

## Report

## Comments

### 2026-11-07, claude-audit
Created from the owner's question about hard-coded lists (verified: Together has 5 FLUX entries in `builtin-providers.json`). The Together API response format was not found in the docs; a live request with a key is required. Plan for Luna.

### 2026-11-07, owner (in chat, recorded by claude-audit)
Proposal approved ("Yes"); the plan was approved under the earlier instruction "approve all tasks that come up".
