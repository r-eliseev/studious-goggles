# 0020. A shared in-memory adapter for synchronous image transports

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-30

## Context
ADR 0005 describes a separate `OpenRouterJobRunner` with its own semaphore and local queue. Tasks 111/102/128/129 require a shared path for the synchronous image transports of OpenRouter and OpenAI-compatible providers, and the provider semaphore must belong to `GenerationService`. Owning the semaphore in two places and different queues would conflict; a queue position before the actual submit cannot be promised reliably either.

## Decision
This contract supersedes ADR 0005:

1. One `InMemoryQueuedImageProvider` adapts the synchronous image transports of OpenRouter and OpenAI-compatible providers to the queued contract. The adapter does not own a semaphore and does not promise a queue position.
2. The only per-provider semaphore belongs to `GenerationService`. A job waits for a slot before submit and holds it from submit until a terminal state or local cancellation. Before submit the database generation may have the Queued status; the adapter handle appears only after submit.
3. `GenerationService` owns the CTS of the local job; the adapter owns the CTS of the active transport. Cancellation links these levels through the handle per the contract of task 121.
4. The adapter keeps a terminal snapshot in process memory for 10 minutes. The expiry check runs during adapter operations; there is no separate background timer. Removal happens at elapsed ≥ TTL. A process-local handle unknown after a restart is polled as Interrupted; the request is not sent again.
5. Polling does not delay the first request. For native fal the interval is limited to 1–5 seconds; for the in-memory adapter to 50–250 milliseconds. The existing fal network backoff/retry stays. Local mode is determined by the `InMemoryQueuedImageConnection` connection wrapper, not by the provider id, so it also works for custom compatible ids.
6. The composite ProviderRouter and the single queued path of GenerationService are switched on in production atomically by task 129. Task 128 integrates the composite router but does not enable the queued capability in production dispatch.

## Alternatives
- Keep the OpenRouter-specific runner of ADR 0005: it duplicates the queue and the semaphore and does not cover other OpenAI-compatible transports.
- Keep the ordinary synchronous path for some providers: GenerationService would have to branch by transport/provider, keeping different lifecycle and cancellation semantics.
- Keep a separate local queue position: before submit the actual position relative to the provider semaphore and other work sources is unknown; this would promise an unreliable value.
- Delete the terminal snapshot right after the first poll: a client that recovers with a delay would see Interrupted instead of the saved result; the TTL gives a limited window for the handler to finish.

## Consequences
One owner limits the provider's concurrency, and the shared adapter unifies local submit/poll/cancel without separate queues per transport. Terminal snapshots and bytes need bounded memory and timely cleanup; process-local jobs do not survive a restart and are shown as Interrupted. The implementation is spread over tasks 102, 128, 129 and the lifecycle/cancellation tasks.
