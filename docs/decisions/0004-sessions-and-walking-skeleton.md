# 0004. Sessions with an assistant and a walking skeleton on OpenRouter

Status: accepted
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-27

## Context
The owner changed the main scenario: instead of a composer with a prompt and a model set, the user works in a chat with an assistant, and the model set becomes a session setting (`docs/specs/chat-sessions.md`). The first version must be a walking skeleton: a minimum of features, but all systems (secrets, catalog, assistant, generation, storage, UI) work end to end. The skeleton tasks must be doable by weak models without interpretation.

## Decision
- The main unit of work is a session (chat). A run is one launch of a prompt on the session's models with shared seeds. A generation stores the exact prompt, model and seed.
- The assistant is an OpenRouter LLM with one tool `generate_images(prompt)`. An answer without a tool call is treated as text to the user (including a question). The generation starts immediately.
- Skeleton (v0.1): only OpenRouter and only MAUI (Windows). In the skeleton the provider is described by the narrow interface `IImageGenerator.GenerateAsync` (OpenRouter's synchronous model). The submit/watch contract of ADR 0003 is introduced together with fal and Replicate; until then ADR 0003 stays a proposal.
- Generation events in the skeleton are an ordinary .NET event `GenerationUpdated` of a singleton service; MAUI moves them to the UI thread. The subscription through a `Channel` from `docs/architecture.md` is postponed until the Web head.
- Tests: xUnit v3 and NSubstitute for substitutes.
- Packages and versions are pinned in `ImageGen/Directory.Packages.props` (task 001).

## Alternatives
- The composer with model sets from the main specification: the owner chose the chat.
- The full ADR 0003 contract right in the skeleton: complicates the first version for the sake of providers it does not have.
- Moq instead of NSubstitute: the owner does not mind; NSubstitute reads better in tests.

## Consequences
Easier: a short path to a working app, clear tasks. Harder: when adding fal and Replicate, `IImageGenerator` must be extended to the ADR 0003 contract and the events moved to a subscription; the skeleton has no job recovery after a restart (only the "Interrupted" mark).
