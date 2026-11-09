# 0017. Together AI instead of Replicate

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-29

## Context
ADR 0003 and the target layout in `docs/architecture.md` named Replicate as the third image provider. Task 047 (research, `tasks/DONE.md`) compared replacements: Together AI, Black Forest Labs, Stability AI, Ideogram. The task's recommendation was Together AI: `POST https://api.together.ai/v1/images/generations` with `Authorization: Bearer` in the OpenAI format, the answer `data[]` with `b64_json`, FLUX models available. Prices were taken only from aggregators and differ almost twofold; there was no test call with a key.

## Decision
The owner (2026-10-29): the third provider is Together AI instead of Replicate. It is connected as an OpenAI-compatible provider in `providers.json` (ADR 0009, the `OpenAiCompatibleClient` client, the `b64_json` answer), without a separate client. The ADR 0003 contract does not change; this record only refines the choice of provider.

## Alternatives
- Replicate: needs a separate client for asynchronous predictions.
- Black Forest Labs, Stability AI: their own endpoints and schemas, separate clients; response shapes not verified.
- Ideogram: answers with a link with a limited lifetime, a separate client.

## Consequences
- No new client is needed; the connection task is a provider entry in the config, a price check on `together.ai/pricing` and one test call with the owner's key.
- The price and the response shape on the live service are not verified (`docs/status.md`, "Open questions").
- Together answers synchronously, like OpenRouter: it does not take part in the fal queue (ADR 0003).
