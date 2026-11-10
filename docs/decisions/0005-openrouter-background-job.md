# 0005. OpenRouter as a background job and shared status polling

Status: superseded by 0020
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28
Superseded by: [ADR 0020](0020-common-in-memory-queued-adapter.md).

Refines ADR 0003 (the "Decision" section, the OpenRouter handle and `CancelAsync`).

## Context
ADR 0003 fitted the synchronous OpenRouter (`POST /api/v1/images` holds the connection until the picture is ready) into submit/watch like this: the handle "contains the ready answer". Then OpenRouter's `SubmitAsync` waits for the whole generation, while at fal and Replicate it returns immediately. The orchestrator has to tell providers apart, an OpenRouter job cannot be cancelled, and its "queued" and "running" states are nominal. The owner (2026-10-28): the OpenRouter generation is started in a separate task, so that it does not block the UI and fully repeats the behavior of the other providers, and the status is obtained by real polling.

## Decision
1. **The OpenRouter job in the background.** OpenRouter's `SubmitAsync` does not wait for the answer. It registers the job in process memory (`OpenRouterJobRunner`, a singleton), starts the HTTP request in a separate task (`Task.Run`) with its own `CancellationTokenSource` and returns a handle right away: the provider id and the local job id. The handle holds no bytes.
2. **Job states in memory:** "queued" (waits for a slot; the local position is known) → "running" (the HTTP request is sent) → "done" (bytes, type, price) / "failed" (`ErrorKind`, text) / "filtered" (moderation refusal) / "cancelled". Slots are limited by the provider's semaphore (as now `MaxParallelGenerations = 4`).
3. **Shared polling for all providers.** Each provider gives one state-reading operation: `PollAsync(handle) → JobUpdate`. At fal and Replicate it is an HTTP status request, at OpenRouter a read from `OpenRouterJobRunner`. There is one polling loop for all (`ProviderJobPoller`): an increasing interval, an event on every state change, the end on a terminal state. `WatchAsync` from ADR 0003 is built from `PollAsync` by this loop. Intervals are in the provider's config (short for OpenRouter: polling is local).
4. **Result.** OpenRouter's "done" returns bytes from memory; the orchestrator saves the file and the record before the event (as for all). After the terminal state is delivered, the job is removed from memory; terminal jobs that were not picked up are removed after a timeout.
5. **Cancellation.** OpenRouter's `CancelAsync` cancels the HTTP request through the `CancellationTokenSource`: the job goes to "cancelled". Whether OpenRouter charges for an interrupted request is unknown; the card shows the price of such a job as "unknown".
6. **Restart.** The handle is saved to the DB right after `SubmitAsync`, as for all. The in-memory job does not survive a restart: `PollAsync` with an unknown local id returns "failed, `ErrorKind.Interrupted`". There is no separate OpenRouter branch in recovery.

## Alternatives
- **A handle with the ready answer (the original text of ADR 0003).** Simpler, but the orchestrator tells providers apart, there is no cancellation and no honest states.
- **Only a completion event, without polling.** Less work for OpenRouter, but two mechanisms in the orchestrator (an event for some, polling for others). The owner chose the same polling.
- **A persistent OpenRouter queue in the DB.** Recovery after a restart is impossible anyway: a synchronous HTTP request cannot be continued.

## Consequences
Easier: `GenerationService` works the same way with all providers (submit → save handle → poll → save result → event), the UI never waits for a provider, OpenRouter gets cancellation and a position in a local queue. Harder: OpenRouter has its own in-memory job store and cleanup of finished jobs; the picture bytes live in memory until polled. Implemented by task 025 together with the ADR 0003 contract.
