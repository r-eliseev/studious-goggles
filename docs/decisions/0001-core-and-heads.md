# 0001. A shared core and several heads

Status: accepted (2026-10-28)
Allowed statuses: proposed, accepted, superseded by NNNN, rejected
Date: 2026-10-27

## Context
The owner's notes describe one core (Provider, Generation, Config) and several UIs: Android/iOS, Win/Linux/macOS and a Web API with static Vue. MAUI does not support Linux and does not run in Docker, while in the sample project all logic and models live in one MAUI project and are partly tied to UI types (`Brush`, `Color`) and MAUI Essentials (`Preferences`, `FileSystem`).

## Decision
Move the core into a separate `net10.0` library without UI dependencies and make MAUI and Web heads over it. Describe platform things (secrets, settings, data folder, gallery) as interfaces in the core and keep the implementations in the heads. The project layout is in `docs/architecture.md`.

## Alternatives
- Everything in one MAUI project, as in the sample: simpler, but Web and Docker are impossible without duplication.
- Only Web (Vue) without MAUI: covers Linux and Docker, but not saving to the Android gallery and the native clients from the notes.
- Blazor Hybrid or another UI stack: not considered, it is not in the notes.

## Consequences
Easier: shared business logic, core tests without an emulator, a Docker build of Web. Harder: more projects, interfaces for platform functions, the rule "Core does not reference UI" must be checked automatically. The decision depends on the answers to the open questions in `docs/status.md` (Linux, Img2Text, result repository).
