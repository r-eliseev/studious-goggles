# 0003. The provider contract and the job lifecycle

Status: accepted (2026-10-28)
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-27
Refined by: `0005-openrouter-background-job.md` (OpenRouter as a background job with polling). Outdated in the text: "all three providers at once" was cancelled by ADR 0004; the contract is confirmed by tasks `tasks/backlog/025` and `026`, not `tasks/v0.1/`.

## Context
fal works through a queue, Replicate through asynchronous predictions, OpenRouter answers synchronously with a base64 picture (table in `docs/specs/image-client.md`). Statuses, cancellation, price and filtering differ for each. The phone app can be unloaded by the OS in the middle of a generation, and the client has no public address for a webhook. The owner decided to do all three providers at once, so that the contract is checked against all three calling models.

## Decision
One `IImageProvider` interface in `Core/Providers`:
- `CheckKeyAsync` → works, invalid key, no connection (F1.2).
- `ListModelsAsync`, `DescribeModelAsync(modelId)` → `ModelDescriptor` with capabilities, the reference limit, the price and its unit. Source of capabilities: at OpenRouter `GET /api/v1/images/models`, at fal and Replicate the model's input schema.
- `SubmitAsync(ProviderJobRequest)` → `ProviderJobHandle` (serializable: the fal request id, the Replicate prediction id; at OpenRouter the handle contains the ready answer).
- `WatchAsync(handle)` → `IAsyncEnumerable<JobUpdate>`: queued (with the position, if any), running (with logs), done (links or bytes, seed, actual price if any), failed (`ProviderErrorKind` and the original text), filtered.
- `CancelAsync(handle)` → `CancelOutcome`: cancelled, request sent (fal in progress), not supported (OpenRouter).

One job state model: queued, running, done, failed, cancelled, cancellation request sent, filtered. "Filtered" is a separate state (a black frame with `has_nsfw_concepts` at fal, a moderation refusal at others), not an error.

The orchestrator (`GenerationService`) saves the handle to the DB right after `SubmitAsync`, so polling continues after a restart. Polling with an increasing interval; no webhook. The limit of concurrent jobs per provider is a semaphore. The result is saved before the event goes to the head.

Provider errors are mapped to `ProviderErrorKind`: invalid key, no funds, rate limit, model unavailable, rejected by moderation, timeout, network failure, other (F9.1). The provider's text is kept, keys are cut out of it.

## Alternatives
- Separate interfaces per kind of generation (`ITextToImage`, `IImageToImage`, `IImageToText`, as in the drafts): the kind of generation is a property of the request and a capability of the model, while the provider's calling model is the same, so the interfaces would duplicate each other.
- Webhook: needs a public address, a phone does not have one.
- A blocking call per job (subscribe at fal, `Prefer: wait` at Replicate): simpler, but loses the queue position, cancellation and recovery after a restart.

## Consequences
Easier: the orchestrator, the UI and the tests work with one model; a new provider is added with one implementation. Harder: OpenRouter has to be fitted into submit/watch (a handle with a ready answer), and its job cannot be recovered after a restart (it is marked "interrupted"). The contract is confirmed by the provider tasks in `tasks/v0.1/`; if the implementation shows a discrepancy, the decision is changed by a new ADR.
