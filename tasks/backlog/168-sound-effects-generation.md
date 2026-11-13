---
id: 168
status: blocked
assignee:
claimed:
created: 2026-10-31
---

# Sound and sound effect generation

## Description
Owner decision of 2026-10-31: music, sound and video generation are tasks for later releases (video and audio were previously out of scope: `docs/product.md`, "Out of scope"; `docs/specs/image-client.md`, "Out of scope").

Short sounds and effects (footsteps, rain, interface sounds) follow the same flow as music (167), but with a short duration and, if supported by the model, looping.

Depends on: 166 (done), 279 (fal sound-effect facts); the provider is fal (ADR 0035).
Environment: Linux or Windows; both UI shells.

## Acceptance criteria
- Sound effects models are available in the catalog (following 166), with a price or "price unknown".
- Duration parameters (and looping if supported by the model); save the result immediately and show a card with a player and actions, as in 167.
- Compare several variants of one request (models × seeds) by listening to them in sequence.
- Both shells; player accessibility.

## Analysis
Scope and models will be clarified after 166. If music and sound use the same models, this task can be combined with 167 during planning.

## Plan

## Report

## Comments

### 2026-10-31, owner (in chat, recorded by claude-audit)
Add sound generation to a future release.

### 2026-11-06, claude-audit
Blocked: the owner selected OpenRouter only (ADR 0027), but it has no sound effects models (the `/api/v1/models` catalog on 2026-11-06 listed only Lyria 3 and gpt-audio for audio). Options for the owner: wait for a model on OpenRouter; generate sounds with Lyria (quality for short effects is unknown; check after 208); or add fal (ElevenLabs Sound Effects v3, Stable Audio 2.5, and others; `docs/knowledge/media-generation.md`).

### 2026-11-13, owner (in the chat, recorded by claude-planner)
Asked how to unblock sound effects (wait for OpenRouter, try Lyria, or add fal): "Add fal for sound". Recorded as ADR 0035. The models, request/result shapes and the integration are unknown, so the fact-finding task 279 goes first; this task stays `blocked` until 279 is done, then the planner writes its plan from the findings.
