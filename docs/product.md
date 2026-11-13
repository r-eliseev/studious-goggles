# Product: ImageGen

The full functional specification is `docs/specs/image-client.md` (features F1–F10, screens, entities). This file holds only the owner's goals, boundaries and decisions that did not go into the specification. Source of the additions: the owner's handwritten notes of 2026-10-27, retold below.

## Why the project exists
One client for generating images through the built-in fal.ai, OpenRouter and Together AI (ADR 0017; Together is available only for images, a live response has not been verified yet), plus additional OpenAI-compatible providers from the config. The user connects their own keys (BYOK) once, sends one prompt to several models at once and sees the results side by side with price and time. The result is saved on the user's side immediately, because provider links expire.

## For whom
One user with their own keys (decided: there is always one user). In the Web variant, login is a form; the password is set by an environment variable.

## Principles (from the specification)
- BYOK: the app does not stand between the user and the provider in money matters.
- One prompt, many models: a parallel run is the main scenario.
- Common parameters are translated into each provider's parameters; what is not supported is shown explicitly.
- The result is never lost: it is saved locally right after it is ready.
- Transparency: provider, model, parameters, time and price are visible for every run; filtering by the provider is a separate state.
- Provider moderation is not bypassed.

## Owner's decisions (notes, the "core + heads" scheme)
The core (Core) does not depend on the UI and has three parts:
- **Provider**: built-in fal, OpenRouter and Together AI for images; other OpenAI-compatible providers are defined in the config (ADR 0017).
- **Generation**: Text2Img, Img2Img, Img2Text.
- **Config**: Secrets and Preferences.

Heads (UI) over one core:
- Android / iOS;
- Windows / Linux / macOS;
- Web API + a static Vue front end.

Additional requirements from the notes:
1. Automatic loading of the providers' model lists.
2. Encrypted secret storage.
3. Simple web authorization: the password is set by an environment variable.
4. Docker support (first of all for Web API + Vue).
5. Prompt expansion (improvement on request; in the specification it is F4.8, P2, "before and after", the user decides).
6. Our own result repository (result storage under our control, not just files on disk).
7. Android: saving files to the gallery.
8. Adding models through the config (in the specification F2.5: a fal model by identifier; an OpenAI-compatible provider lists its models in the config).

Decided 2026-10-27: Img2Text (a text description or prompt from a finished picture) is part of the first version; "our own result repository" is a local DB + files behind an interface in Core; Linux was postponed (MAUI does not support it); revised 2026-10-28 — the Linux desktop is provided by the Avalonia head (ADR 0006). Everything else that is ambiguous: `docs/status.md`, "Open questions".

## Sessions with an assistant (decision of 2026-10-27)
The main scenario is a chat, as in ChatGPT: the user describes a picture, the assistant (an LLM through OpenRouter) writes the prompt and immediately starts the generation, changes the prompt on request and runs again, and asks back when needed. The list of models and the number of runs per model are session settings; they can be changed at any time. Every generation stores the exact prompt, model and seed; it can be repeated, regenerated with a new seed, or its prompt can be edited by hand. Details: `docs/specs/chat-sessions.md`, ADR 0004.

The first version is a walking skeleton on OpenRouter and MAUI (Windows) with a minimum of features but with all systems working. The base UI language is English.

## Owner's decisions (2026-10-27 — 2026-10-31)

- **Platforms and heads:** MAUI for Android, iOS, macOS and Windows; Avalonia for the Linux desktop (ADR 0006) and Android next to MAUI for comparison (ADR 0026); Avalonia on iOS and macOS — CI builds and tests only, no publishing (ADR 0030); Web API + Vue for the browser and Docker. Decisions on building and manually checking specific environments are recorded in `docs/knowledge/dev-environment.md`.
- **Secrets and data:** Web/Docker keys are stored in a file protected by ASP.NET Core Data Protection with the keys in a Docker volume. User data is a local DB and files behind a Core interface; there is no synchronization between devices. Android is excluded from cloud backup.
- **Language and scenario:** the UI is in English; generation prompts are in English; the assistant answers in the user's language. The main scenario is the chat with the assistant; the first version was a skeleton on OpenRouter and MAUI.
- **Repository language (decision of 2026-11-09):** all documentation, tasks, code, comments and Gherkin scenarios are in English; the harness check rejects non-Latin text in the documentation (`docs/conventions.md`, "Language").
- **Providers:** Together AI was chosen instead of Replicate (ADR 0017) as a built-in image-only provider. A live response and billing remain unverified; contract details and the current catalog are in `docs/knowledge/providers.md`.
- **Spending:** the ledger records all paid calls with a known price — images, assistant replies, summaries and descriptions.
- **Semantic search:** when it is turned on, the whole existing history is indexed after the user confirms the volume and the cost; a separate mode next to word search. Text only and `baai/bge-m3`, no offline fallback (ADR 0016).
- **UI and accessibility:** all three directions Quiet, Gallery and Notebook are available as switchable styles (ADR 0014). Platform guidelines and sizes of interactive elements are collected in `docs/design/implementation.md`.
- **Other boundaries:** one user; Web login is a password from an environment variable; macOS parts are checked against documentation while there is no Mac. Generation of music, sounds and video is planned for later versions (decision of 2026-10-31, tasks 166–169); the first versions stay image-only.
- **Tooling:** adopted CommunityToolkit.Maui (ADR 0018), xUnit v3/NSubstitute, ADO.NET and our own SQLite migrations (ADR 0002). Syncfusion.Maui.Toolkit is considered first if charts, shimmer or pull-to-refresh are needed; the package is not used yet. Acceptance scenarios are Gherkin on Reqnroll (ADR 0031).

## Boundaries: what we do
The first version per the specification: generation from text, editing from references, parallel run and comparison, gallery, spending tracking. Plus the owner's decisions above, as far as they do not contradict the specification.

## Boundaries: what we do not do
Video and audio in the first versions, LoRA training, synchronization between devices, local models (ComfyUI), a canvas editor with masks, multi-user mode, bypassing provider moderation, hidden substitution of the prompt or model (F9.4).

Revised 2026-10-31: generation of music, sounds and video is planned for later versions (tasks 166–169); the first versions are images only. 2026-11-06: video and music go through OpenRouter, the player in Avalonia is LibVLC (ADR 0027, tasks 199–209); sound effects wait for models at OpenRouter (168). 2026-11-13: sound effects go through fal (ADR 0035; fact-finding 279, then 168).

## Terms
- **Provider**: fal.ai, OpenRouter, the built-in Together AI for images, or an OpenAI-compatible provider from the config (ADR 0017).
- **Run group**: one press of "Run"; contains one job per model.
- **Job**: the run of one model in a group; states: queued, running, done, failed, cancelled, filtered.
- **Model set**: a named list of models for a parallel run.
- **Core**: the library without UI dependencies (providers, generations, configuration).
- **Head**: an app over the core: MAUI, Web API + Vue.
- **BYOK**: the user's own keys.
