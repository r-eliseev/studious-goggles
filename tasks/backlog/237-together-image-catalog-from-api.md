---
id: 237
status: blocked
assignee:
claimed:
created: 2026-11-07
---

# Together AI: load the image model catalog from the API instead of a built-in file

## Description
The owner's question of 2026-11-07 about hard-coded lists: Together AI image models are five FLUX models listed in `ImageGen/ImageGen.Core/Providers/builtin-providers.json` (`imageModels`); `OpenAiCompatibleClient.ListImageModelsAsync` returns them from the config. The list should be loaded from the Together API, as it is for OpenRouter and fal.

The response format for the Together model-list API was not found in its documentation (https://docs.together.ai, 2026-11-07): the docs have catalog tables and the SDK has `client.models.list()`, but there is no description of response fields. Therefore, the first step is a live request with the owner's Together key; without the key, the task cannot proceed beyond step 1.

Depends on: —
Environment: Linux or Windows; live request requires the owner's Together AI key (the model list is free).

## Acceptance criteria
- Step 1 (facts): with a key, call `GET https://api.together.xyz/v1/models` (or `https://api.together.ai/v1/models`, as set by `baseAddress` in `builtin-providers.json`) and record in `docs/knowledge/` (a new “Together AI” section in `docs/knowledge/media-generation.md` or a nearby file): response shape (array or object), model fields (`id`, model type such as `type`, name, price and unit), and which models are image models. Without a key, stop and record in “Comments” that the key is needed.
- If the response identifies image models: `OpenAiCompatibleClient.ListImageModelsAsync` for a provider with the new `imageModelsFromApi: true` config flag (Together) loads image models from the API (name, price if present, `SupportsSeed` as for other providers without an explicit flag: `false` if the API does not provide it); Together's `imageModels` config remains a fallback list if the API fails (with a Warning). Other OpenAI-compatible providers in the user's `providers.json` remain unchanged.
- If the response does not identify image models, keep the config list, record the fact, and close the task with this conclusion.
- Test against a fixture of the real response (reduce it to 3–5 models; do not save the key or personal data): image models are selected and other models are excluded; an API error falls back to the config list and logs a Warning.

## Analysis
`ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs` (`ListImageModelsAsync` from `Config.ImageModels`), `ImageGen/ImageGen.Core/Providers/builtin-providers.json`, and the provider config type (`ProviderConfig`; validation in `ProviderRegistry` — the new field must pass validation and be added to the config specification in `docs/specs/image-client.md` if it describes the `providers.json` format).

## Plan
1. Make the live request and record the facts (step 1).
2. Based on the result, add failing tests using the fixture, the config flag, and API loading with a fallback list; or close the task with a conclusion.
3. Update the config documentation.

Files: `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core/Providers/builtin-providers.json`, `ImageGen/ImageGen.Core/Providers/ProviderConfig.cs` (or wherever the config is defined; `rg -n "class ProviderConfig" ImageGen`), `ImageGen/ImageGen.Core/Providers/ProviderRegistry.cs`, tests in `ImageGen/ImageGen.Core.Tests/`, `docs/knowledge/media-generation.md`, and, if needed, `docs/specs/image-client.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
bash scripts/check.sh
```

### Execution order (for Luna)
- First read only the files in the “Files” line and the named tests; compare signatures with the current `main`.
- Follow the steps in order (ub-mode): add a failing test → run it and confirm it fails for the stated reason → make the smallest fix → verify the test passes.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong in separate tasks.
- If the code differs from the plan (missing method, different signature, or an existing test), do not improvise: stop and record a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In “Report”, record what changed, commands and results, and deviations from the plan.

## Report

## Comments

### 2026-11-13, codex-gpt6
The required live API check cannot proceed because this environment has no Together AI credential (checked environment variable names; no Together key is configured). The plan explicitly requires a live authenticated request as step 1 and says to stop without a key. No request was sent.

Unblock when the owner provides/configures a Together AI key for this session; then perform the documented free model-list request and continue from step 1.


### 2026-11-07, claude-audit
Created from the owner's question about hard-coded lists (verified: Together has five FLUX models in `builtin-providers.json`). The Together API response format was not found in the docs; a live request with a key is needed. Plan for Luna.

### 2026-11-07, owner (in chat, recorded by claude-audit)
Proposal approved (“Yes”); the plan was approved under the earlier instruction “approve all tasks that come up”.

### 2026-11-13, owner (in the chat, recorded by claude-planner)
Asked whether to add a Together key, postpone or cancel: "Postpone". The task stays `blocked` until the owner provides a Together key; the plan is unchanged.
