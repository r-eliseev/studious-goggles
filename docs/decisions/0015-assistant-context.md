# 0015. Assistant context: a budget from the model's context and a summary in a separate table

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-28

## Context
Now the assistant gets the last 20 messages of the session (S2.6). A long session may not fit into the model's context, and early agreements (style, refusals) are lost. Task 027; the owner's decisions are in its "Comments" and in `docs/specs/assistant-context.md`.

## Decision
1. The request budget is counted from `ChatModel.ContextLength` (the model's whole context, no ceiling); tokens are estimated by text length with a correction from `usage.prompt_tokens` of the last answer (`docs/specs/assistant-context.md`, C1–C2).
2. The old part of the history is compressed into one rolling summary per session by the same assistant model, in the background after the answer (C3).
3. Storage: the `conversation_summaries` table, one row per session (overwritten): `session_id` (PK, FK `sessions(id)` `ON DELETE CASCADE`), `text`, `covers_up_to_message_id`, `provider_id`, `model_id`, `cost_usd` (NULL if the provider did not say), `created_at`. A new migration (ADR 0002), `0001_initial.sql` is not changed.
4. Logic without I/O (budget, choosing messages, shortening) is pure classes in `ImageGen.Core/Context/`; I/O is in `ConversationSummaryService` and the repository.
5. The chat model catalog stays in memory (`ChatModelCatalogService`): no need to move it to SQLite, `ContextLength` is already there.

## Alternatives
- **A budget ceiling (16 or 32 thousand tokens)** for the sake of price — the owner chose the model's whole context.
- **A summary before the request** — fresher, but the answer waits for two model calls; the owner chose the background.
- **Chained summaries (one per chunk of history)** — more precise, but harder to store and assemble; one rolling summary is enough for one user.
- **Storing the summary as a message in `chat_messages`** — mixes service text with the conversation and breaks the feed filters.

## Consequences
- Long sessions cost more: every message carries as much history as fits into the model's window. The price of summaries is stored for the spending screen (021).
- Parsing `usage.prompt_tokens` is needed in the OpenRouter, OpenAI- and Anthropic-compatible clients.
- The `AssistantPrompts.HistoryMessageCount` constant (20) becomes the minimum of verbatim messages.
