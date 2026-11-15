# Status

Updated: 2026-11-14

Here are open questions, known problems, CI and the task queue; product decisions are collected in `docs/product.md`. Progress is not written here: what is done and what is in progress is shown by task statuses (`scripts/list-tasks.sh`, `scripts/list-tasks.sh --main in-progress`) and their "Reports".

The main scenario is sessions with an assistant (`docs/specs/chat-sessions.md`, ADR 0004). Tasks: `tasks/v0.1/` — the walking skeleton on OpenRouter and MAUI; `tasks/backlog/` — further releases, detailed after the skeleton. Where each task can be done: `docs/knowledge/dev-environment.md`.

CI: `.github/workflows/build.yml` (the `core` job on Ubuntu: formatting, build, tests; `avalonia` on Ubuntu: build and headless tests; `avalonia-android` on Ubuntu: Android Release build; `windows-tests` on Windows; the `maui` job on Windows: build for Windows and Android), `release.yml` (Avalonia Android APK, MAUI APK and the other release packages on a `v*` tag or a push to `main` that changes the shipped code in `ImageGen/`) and `harness.yml`/`harness-audit.yml`. The `maui-android` workload on ubuntu-latest failed with "Disk quota exceeded" (with 24 GB free, the cause is not known), so Android is built on Windows.

## Order of work

Before claiming, always check `Depends on` in the task file: every prerequisite must be done **in main** or in `tasks/DONE.md`. Separately check the environment and other agents' branches/held files per `docs/tasks.md`; the absence of a functional dependency does not allow two agents to edit a held file at the same time.

### Task availability
Current statuses are in the task cards and the output of `scripts/list-tasks.sh`. As of 2026-11-14 (planner state review):
- **Red builds first:** 281 — `build.yml` `windows-tests` is red on `main` since PR #109 (the background catalog refresh keeps `imagegen.db` open); 282 (after 281) — the Windows acceptance step, hidden behind 281, fails on the log file lock (seen on PR #107); 283 — Windows UI E2E `FirstRunWithMock_GeneratesImage` fails since PR #110 (data folder includes the publisher; the job is `continue-on-error`, so the workflow looks green).
- **Waiting for the planner's review:** 255, 256, 257, 259, 261, 262, 263, 279.
- **Product fixes:** 284 (cancel of a submitted synchronous image request → stop waiting, owner's decision of 2026-11-14), then 258 (CAN-03/04 may go first); 280 (expensive-run warning, F8.4); 276 (reject over-long rename).
- **Acceptance scenarios:** 264, 265, 273 (after 255), 267 (after 254–265).
- **E2E on Gherkin:** 266. **Translation:** 269, 271. **Catalogs:** 236. **Apple:** 274 → 247 → 248 → 250. **Windows data:** 230 (amended: the "User Name" publisher folder is a legacy candidate). **Sound effects:** 168 after the review of 279.
- **Blocked for a real reason:** 237 (Together key, postponed by the owner), 168 (until 279 is reviewed and 168 is planned), 247 (after 274).
- **Manual checks** (a human at the screen): 016, 046, 140, 198.
Finished ones are in `tasks/DONE.md`; delete `docs/specs/ui-e2e-original/` when 238–250 are all archived (247, 248, 250 remain).

Before a claim, check the dependencies and the environment in the card. There are no Windows tasks in the queue (088, 139, 148 done 2026-11-01, their manual items are in 016).

### Standing instructions of the owner (ADR 0033–0034)
- **2026-11-07**, in the chat (translated from Russian): "approve all tasks that will come". A task approved by it gets its own record `Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come")`. In force until the owner withdraws it.
- **2026-11-11**, in the chat (translated from Russian; ADR 0034): execute approved tasks and tasks returned for changes one at a time; update the existing PR after each task, do not wait for merges, and keep working across routine messages. This executor authorization remains active until the owner changes or withdraws it. Dependencies, environment limits, active branches, and missing credentials still block the affected task; continue with another independent authorized task where possible.

### Queues by dependency
- Sessions/screen: 021–024 done.
- Web: 031–033 done; event stream fixes 179, 181 done.
- Fullscreen viewer: 131, 139, 146, 148, 149 done (131 closed by the owner's decision of 2026-11-01; manual checks are in 016, 046, 140). 178 — a regression test for Windows keys, the plan waits for approval.
- Android: 140 — manual check. 134/159/160/162/163/164/165 finished; the second run is signed with the same certificate (release `41343960926`, 2026-11-01); updating over it is checked in 140.
- Search: 105 done (fixes 184, 185). The model's dimension, the `usage.cost` unit and RU/EN quality remain unknown until a live check.
- Check other dependencies directly in the task cards; finished tasks are listed in `tasks/DONE.md`.

### Planning, real blockers and manual checks
- 021–024, 031–033, 105 done; a live paid measurement of embeddings and a labeled RU/EN check were not done.
- 030 partly done at the owner's request of 2026-11-02: the name Omnilite, the assets and `com.eliseev.imagegen` were applied to all MAUI targets. The publisher edit is approved (owner's values of 2026-11-13); the data and secret folders after the id change are task 230.
- 016 — manual MAUI Windows, 046 — Avalonia Linux desktop/Windows, 140 — Android. Done by a human at the screen. Additional unverified scenarios of ordinary code tasks are handed over there per `docs/tasks.md` and do not keep a finished code task in-progress. The historical comments of 016 are kept; its Android follow-ups belong to 140.

Finished tasks and earlier waves are in `tasks/DONE.md`. The statuses of the task files remain the source of truth; the queue above does not mean a task is already done.

## Open questions
- Apple CI: task 249's three consecutive hosted macOS runs confirm a GUI session, Accessibility access, and nonuniform screenshots. Proposal: create a separate task for UI tests on macOS with Appium Mac2 4.6.1; owner decision pending. Task 247 has two successful Avalonia simulator runs followed by a failure after launch (`aliveAfter30s=false`, no crash report); inspect `launch.log` and rerun before enabling PR smoke jobs.
- OpenRouter had no free image models as of 2026-10-27; the default model is `black-forest-labs/flux.2-klein-4b` ($0.014 per megapixel, has seed). The default assistant model was originally `anthropic/claude-haiku-5.5`; the setting for new sessions is stored in `defaults.json` (see `docs/specs/chat-sessions.md`, S1.3).
- fal model prices (task 093): the model list has no prices, `GET https://api.fal.ai/v1/models/pricing` without a key answers 401, the response shape is unknown; fal shows "price unknown". Check with a fal key.
- API shapes verified only against stubs (task 036): the `images/generations` response of OpenAI-compatible services (`data[0].b64_json` is used, a URL response is not supported; `response_format`/`output_format` are configured per ADR 0023); the `anthropic-version: 2023-06-01` header and `GET /models` of Anthropic-compatible services. Whether they match the real services is unknown: check by hand with a real key (`docs/decisions/0009-provider-config-and-model-ref.md`).
- Image description (task 019): the answer of a model with an image on live OpenRouter and the price of a description are not verified (ADR 0013); whether failed chat completions are billed is unknown.
- Attachments (task 018): the `input_references` shape in `POST /api/v1/images` is verified only by a stub (the owner's assumption, ADR 0012); the F10.6 consent is kept in the chat screen's memory; attachment thumbnails are not shown in the feed.
- Generation parameters (task 020): the body shape of `POST /api/v1/images` for `aspect_ratio`, `resolution`, `output_format`, `quality` is verified only by a stub (the owner's assumption, ADR 0011). The unit of the resolution values `512` and `768` is not confirmed.
- Logging (task 045): the Avalonia log folder path on macOS is not verified (Windows verified 2026-10-28) (`docs/knowledge/dev-environment.md`, "Logs"; ADR 0010).
- Starting the app with an invalid `providers.json` (error message and stopping the startup) is verified by a composition test, but not in a MAUI or Avalonia window.

- Semantic search (ADR 0016, 105): the owner decided to index the whole history at once when it is turned on and to use a separate search mode. The actual dimension of `baai/bge-m3`, peak memory and the `usage.cost` unit of embeddings remain unknown; checking requires a live request with a key.
- Task 030: ApplicationId `com.eliseev.imagegen`, the name Omnilite and the MAUI icon/splash assets were provided/approved by the owner and applied 2026-11-02. Windows Publisher/PublisherDisplayName given by the owner on 2026-11-13 (`CN=Roman Eliseev` / "Roman Eliseev", task 030). Remaining: a check of the actual data/secret path of MAUI Windows after the id change (task 230); old data was neither copied nor deleted. The new Android sandbox gets no automatic access to the old one.
- Together AI: a built-in image-only provider with a FLUX catalog; the official contract/prices were reconciled with the documentation 2026-10-30. A live paid response, availability per account and actual billing were not verified; `CostUsd` remains unknown.

- OS fullscreen for MAUI iOS/MacCatalyst: who provides a compatible macOS/Xcode environment and a device check? Until a separate verifiable plan, the modal viewer of 131 stays; OS fullscreen on these platforms is not claimed.
- Apple CI (task 247): after three successful nightly iOS smoke runs, enable the smoke jobs on pull requests? For now the schedule and the manual run stay separate from PRs.
- Replacing the E2E tests (owner, 2026-11-09: "we are essentially replacing the current e2e tests with these"): understood as rewriting the E2E tests as Gherkin scenarios that run on Reqnroll on top of the same Appium/FlaUI drivers (task 266), and replacing the Avalonia flow tests with acceptance scenarios (task 267). Whether the Appium/FlaUI runs of the built apps should be dropped completely is not decided; the assumption is that they stay.

## Known problems
