---
id: 279
status: approved
assignee:
claimed:
created: 2026-11-13
---

# fal sound effects: models, schemas and the queue contract (fact-finding)

## Description
ADR 0035 (owner, 2026-11-13): sound effects go through fal. Before task 168 can be planned, the facts must be collected: which fal models make sound effects, their request fields, their result shape, and how they fit the existing fal queue client (`ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`). `docs/knowledge/media-generation.md` ("fal.ai and Together") has only one example (ElevenLabs Sound Effects v3: `text`, `duration_seconds` 0.5–30, `loop`). The catalog and the model schemas are public (no key needed); prices need a key.

Depends on: —.
Environment: Linux with access to `api.fal.ai` (and the fal documentation pages); no fal key needed. No product code.

## Acceptance criteria
- `docs/knowledge/media-generation.md`, a new section "fal: sound effects (2026-MM-DD)" with, for every model of `GET https://api.fal.ai/v1/models?category=text-to-audio` whose name or description is about sound effects (not music, not speech): the endpoint id, the display name, the input fields with type, range and default (from `?endpoint_id=<id>&expand=openapi-3.0`), whether looping is supported, the output fields (`audio.url`, `content_type`, `file_size` or other), and the audio format(s) declared. Paginated catalog responses are followed to the end; the number of models seen in the category is stated.
- The queue contract compared with `FalImageProvider`: submit, status and result URLs and fields for one sound model, from its OpenAPI schema; whether it is the same `queue.fal.run` flow the image provider uses; differences listed.
- Prices: one line — "unknown without a key" unless the public pages state a price (quote and link).
- A proposed integration in "Comments" (the planner turns it into 168's plan): which 1–3 models to support first and why, the field mapping to a sound request (prompt, duration, loop), where it plugs into the existing audio path (`IAudioModelProvider`, `IAudioGenerationProvider`, `AudioModelCatalogService` are OpenRouter-only today — name the members that assume OpenRouter), and the open questions.
- No product code, no new packages.

## Analysis
- Existing fal code: `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`, `FalUrlValidator.cs`; audio path: `ImageGen/ImageGen.Core/Providers/IAudioModelProvider.cs`, `IAudioGenerationProvider.cs`, `AudioGenerationRequest.cs`, `ImageGen/ImageGen.Core/Services/AudioModelCatalogService.cs` (`ProviderIds.OpenRouter` hard-coded).
- Raw responses may be saved only under `tmp/` (not committed); quote the relevant fields in the knowledge section.

## Plan
1. Fetch the catalog for `text-to-audio` (all pages); list the sound-effect models (criterion: name/description mentions sound effects, SFX, foley or ambience; list the borderline ones separately).
2. For each, fetch the OpenAPI schema; record inputs and outputs.
3. Compare the queue flow with `FalImageProvider` (read the code; do not run it).
4. Write the knowledge section and the proposal in "Comments"; `bash scripts/check.sh`.
If `api.fal.ai` is not reachable from the environment: `blocked` with the exact error (proxy refusal or other), no guessing from memory.

Files: `docs/knowledge/media-generation.md`.

### Verification
```
bash scripts/check.sh
```

## Report

## Review

## Comments

### 2026-11-13, claude-planner
Created after the owner's decision (ADR 0035) to unblock task 168.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
