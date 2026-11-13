# 0035. Sound effects through fal

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-11-13

## Context
ADR [0027](0027-media-generation.md) chose OpenRouter for video and music and left sound effects (task 168) waiting for an OpenRouter model. On 2026-11-06 the OpenRouter catalog had no sound-effect models (`docs/knowledge/media-generation.md`). fal is already a built-in provider with a job queue (`ImageGen/ImageGen.Core/Fal/FalImageProvider.cs`) and has sound-effect models (for example ElevenLabs Sound Effects v3: `text`, `duration_seconds` 0.5–30, `loop`), each with its own field schema; prices come only with a key.

## Decision
On 2026-11-13 the owner chose fal for sound effects (in the chat: "Add fal for sound"). Only sound effects go through fal; video and music stay on OpenRouter as in ADR 0027, whose item 1 is refined by this decision for sounds only. The concrete models, the request and result shapes and the integration (catalog, queue, card) are decided by the planner after the fact-finding task 279; an unknown price is shown as "price unknown" until a check with a fal key.

## Alternatives
- Wait for an OpenRouter sound-effect model — no date; not chosen by the owner.
- Generate short effects with Lyria 3 — quality for effects unknown; not chosen.

## Consequences
- A second media provider for audio: the fal key becomes necessary for sounds; the audio catalog and generation code must accept a provider other than OpenRouter.
- Each fal model has its own schema: the first version supports only the models listed by task 279, with fields mapped explicitly.
