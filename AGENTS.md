# AGENTS.md

## About the project
A multi-provider client for image generation: keys of the built-in providers fal.ai, OpenRouter and Together AI (BYOK) are connected once; Together is built in only as an image provider, and a live response has not been verified yet. Other OpenAI-compatible providers are added through the config. One prompt goes to several models in parallel, the results are shown side by side with price and time and are saved locally. Stack: C# / .NET 10; target layout: a shared core and heads (MAUI for mobile and desktop, Web API + Vue for the browser and Docker). The owner's goals and decisions: `docs/product.md`. Full specification: `docs/specs/image-client.md`. Main scenario (sessions with an assistant): `docs/specs/chat-sessions.md`.

## Language
Everything in the repository is written in English: documentation, tasks, code, comments, test names, Gherkin scenarios, commit messages. `scripts/check.sh` rejects non-Latin letters in the checked paths (`docs/conventions.md`, "Language"). Files that are not translated yet are listed in task 268; until then their Russian section names stay valid for the scripts.

## Commands
- Avalonia data folder: `ImageGen` in local app data by default; the `IMAGEGEN_DATA_DIR` variable sets another one (tests use a temporary one, see task 048).
- App logs: `<data folder>/logs/imagegen-<date>.log` (14 days). Level: the `IMAGEGEN_LOG_LEVEL` variable or `logging.json` in the data folder. Where the folder is on each OS: `docs/knowledge/dev-environment.md`, section "Logs".
- Environment: almost everything runs on Linux — the core, storage, `Presentation`, Avalonia (including Android with the Android SDK and the `android` workload installed) and MAUI through the Android target; the MAUI Windows target is checked by CI. Windows only: launching and manually checking MAUI on Windows (the "Environment" line in the task, `docs/knowledge/dev-environment.md`).
- Core build: `dotnet build ImageGen/ImageGen.Core/ImageGen.Core.csproj`; the whole solution only on Windows with the MAUI workloads (slow): `dotnet build ImageGen/ImageGen.slnx`
- Tests: `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`, `dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj`, `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`, `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj` (headless, on Linux), and `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj` (Reqnroll, headless, on Linux)
- Avalonia build (Linux, Windows, macOS desktop, after task 039): `dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj`; run: `dotnet run --project ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj` (needs a display)
- Avalonia Android build (Linux with the Android SDK): `dotnet workload install android`, `dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Debug` or `-c Release`
- MAUI build (Android — on Linux with the Android SDK and on Windows; the Windows target — only Windows or CI): `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` (and `-f net10.0-android`)
- Running on Windows: after the build, `ImageGen/ImageGen.Maui/bin/Debug/net10.0-windows10.0.19041.0/win-x64/ImageGen.Maui.exe`
- Linter/formatting: for each changed project `dotnet format ImageGen/<Project>/<Project>.csproj --verify-no-changes` (do not format the whole solution: on Linux the MAUI project does not load)
- Harness check (documentation, tasks, language): `scripts/check.sh`
- Staleness signals (audit): `scripts/audit.sh`

## How the harness works

The root contains `ImageGen/` (source projects), `docs/` (specifications, decisions, knowledge), `tasks/` (tasks), `poc/` (isolated proofs of concept) and `scripts/` (checks and task tooling). Tasks are done in `task/NNN-*` branches from a fresh `origin/main`; one task — one branch; after it is finished its PR may be combined with other changes. Before claiming, check other agents' `origin/task/*` branches and the files they hold. The claim/release/new-task scripts commit to the task branch on origin; after a claim, pull that commit with `git pull --ff-only`. The detailed procedure is in `docs/tasks.md`.

## Working procedure (mandatory)
1. Before starting, read `docs/index.md` and `docs/status.md`. Read the rest of `docs/` as needed. Before writing C#/MAUI code, read `docs/conventions.md` and what you need from `docs/knowledge/`.
2. If the user named a task (a number or a file in `tasks/`), work by the rules of `docs/tasks.md`.
3. For large work without a specification, first propose a short specification in `docs/specs/` and wait for confirmation.
4. Implement, run the tests and the linter.
5. Update the documentation (see below).
6. Run `scripts/check.sh`.
7. The work is not finished until steps 4–6 are done.
8. Do every code change in ub-mode: https://github.com/unclebob/ub-mode/blob/master/SKILL.md (TDD: a failing test first, then minimal code; small verifiable steps; commits with the reason and how it was verified). The rules of this harness override the mode where they differ: a branch and a PR instead of `push` to `main` (`docs/tasks.md`); the action log goes to the task's "Report", not to a separate file; the item about pushing to the main branch at the end of the work does not apply.

## What to update when
- `docs/architecture.md` describes the intended structure, not the progress. Edit it only if you implement something differently from what it says (other modules, dependencies, flows) or change the structure itself: then the document must match the code again. Do not write there what was done and by which task: that is the task's "Report" and its status.
- Made a significant decision (choice of a library, data schema, a trade-off) → a new ADR in `docs/decisions/` from the `_template.md` template.
- Changed the behavior of a feature → the corresponding file in `docs/specs/`.
- Found a new non-obvious fact about C#/MAUI or the project → `docs/knowledge/`.
- Finished an audit at the user's request → a `docs/log/YYYY-MM-DD-audit.md` file from the template. Other work outside tasks is recorded as one line in `docs/history.md`. Work on a task is recorded in the task's own "Report".
- Added or renamed a top-level file in `docs/` → update `docs/index.md`.

## Tasks
- Work only on the task the user named. Do not pick tasks yourself.
- The rules for tasks are in `docs/tasks.md`. Read them before working on any task.
- Do not write code without an approved plan.
- The task's "Plan" contains only concepts: contracts, pseudocode, state machines, test cases in words. Do not paste finished code into tasks; proof-of-concept code goes to separate files in `poc/`, and only to remove a real unknown (`docs/tasks.md`, "What to write in the Plan").

## Audit and helper agents

The rules for auditing documentation and code and for delegating to helper agents are in the "Audit" and "Helper agents" sections of `docs/tasks.md`.

## Main code rules
- The core (`Core`) does not reference MAUI, ASP.NET Core or UI types (`Brush`, `Color`, `Shell`); platform specifics come through core interfaces.
- Provider keys and passwords only in encrypted storage; not in `Preferences`, configs, logs, exports or git.
- Provider differences are hidden behind one interface; job statuses are mapped to one model (queued, running, done, failed, cancelled, filtered).
- A generation result is saved immediately when it is received; filtering by the provider is a separate state, not an error.
- MVVM on `CommunityToolkit.Mvvm`, compiled bindings (`x:DataType`), accessibility (`SemanticProperties`) is mandatory.
- SQL only with parameters; `async void` only through `FireAndForgetSafeAsync`; do not block the UI thread.
- Details: `docs/conventions.md`

## Prohibitions
- Temporary files, trial builds and drafts only in `tmp/` at the repository root (it is in `.gitignore`). Do not create files outside the repository. Exception: installing environment tools (SDK, Android SDK, NuGet caches) as described in `docs/knowledge/`.
- Do not change `scripts/` or `.github/workflows/` without an explicit request.
- Do not bypass provider moderation, do not rewrite the prompt and do not substitute the model in secret.
- Do not commit keys, secret files, `bin/`, `obj/`, `.vs/`.
- Do not delete or rewrite existing ADRs (translating them to English without changing the meaning is allowed, owner's decision of 2026-11-09). Mark outdated ones with the status "superseded by NNNN" and a link to the new decision.
- Do not invent facts in the documentation. If you do not know, write "unknown" and add a question to `docs/status.md`.

## If something is unclear
Write the question in `docs/status.md`, section "Open questions", and continue with the safest assumption, stating it explicitly. If the question is about a task, write it in the task's "Comments" (see `docs/tasks.md`).
