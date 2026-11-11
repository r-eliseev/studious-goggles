# Acceptance scenarios (Gherkin)

Status: draft
Code: `ImageGen/ImageGen.Acceptance.Tests` (tasks 252–265); E2E runs: `ImageGen/ImageGen.E2E.*` (task 266)

## Goal
Collect the app's user scenarios in one place, so that:
- the owner reads them as a behavior specification;
- every scenario runs automatically on Linux without keys, network or money;
- it is visible which requirements of the specifications (`chat-sessions.md`, `image-client.md`, `media-generation.md`) are covered.

Tool and level: ADR 0031 — Reqnroll on xUnit v3, headless Avalonia, the real app composition, the network is a scenario fake. Scenarios tagged `@e2e` additionally run in the built MAUI apps through `Reqnroll.NUnit` over Appium/FlaUI (task 266).

## Requirements
1. One `.feature` file per area (the table below), English Gherkin. UI texts in quotes are as in `AppResources`.
2. A scenario has a permanent id from the catalog and tags: `@<id>` and the requirement numbers (`@S3.2`, `@F1.2`). Ids are not reused; a removed scenario is marked "removed" in the catalog.
3. Steps describe the user's actions and observations ("taps New chat", "sees the card in the Ready state"), not internal calls. Preparing the world ("the provider answers with error 500") is done by `Given` steps.
4. A scenario does not depend on the run order, the current date, the culture or randomness: time is a `FakeTimeProvider`, seeds a substituted `ISeedSource`, the culture invariant unless the scenario sets its own.
5. Not a single real network request: an unknown address fails the scenario.
6. A scenario's expectation comes from the specification. If the code behaves differently, the scenario is not adjusted to the code and the code is not fixed in the same task: tag `@ignore @bug-NNN`, a new task with reproduction steps, status "discrepancy (NNN)" in the catalog.
7. A step shared by several areas lives in `Steps/CommonSteps.cs`; do not duplicate a wording with synonyms.
8. `@e2e` marks scenarios that also run on the built apps; their steps must be expressible through `AutomationId`s (`ImageGen.E2E.Common/TestIds.cs`). `@e2e-only` marks scenarios that make sense only on a device (rotation) and are skipped by the headless run.

## World vocabulary (shared steps, task 253)
- "Given the app is launched for the first time" — an empty data folder, no keys.
- "Given the user has a saved OpenRouter key" — the key is in the store, the key check passes in the fake, the default assistant model is chosen.
- "Given the OpenRouter image model catalog: …" (a table: id, name, seed, price per MP).
- "Given the provider answers the generation of model X with: a picture | error N "text" | filtering | nothing until released".
- "Given the assistant answers with: the text "…" | a generate_images call with the prompt "…" | error N".
- "When the user restarts the app" — the same data folder and the same key store, a new composition.
- "Then the app sent no requests except: …" — a check of the fake's log.

## Catalog
Status: "planned" — the scenario is described, not automated; "done" — in a `.feature`, green; "after NNN" — waits for a feature; "discrepancy (NNN)".

### First launch and keys (`FirstRunAndKeys.feature`, task 254)
| id | Scenario: condition → expectation | Requirements | Status |
|---|---|---|---|
| KEY-01 | First launch without a key → the key screen opens, not the session list (`@e2e`, replaces `FirstRunTests.Launch_ShowsFirstRunKeyScreen`) | S1, F1.1 | done |
| KEY-02 | A valid key of the default assistant provider (OpenRouter) → status "works", the session list opens without the model choice (corrected 2026-11-10, task 272) | F1.2, S1.3, S6.4 | planned (272) |
| KEY-12 | On the key screen the user chooses another configured chat provider and saves its key → the model choice of that provider opens; the chosen model becomes the default for new sessions | S6.4 | planned (272) |
| KEY-03 | An invalid key (401) → an "invalid key" message with the provider's name, we stay on the key screen, the key is not saved (`@e2e`, replaces `FirstRunTests.InvalidKey_ShowsError`; on a device the message may also be "no connection") | F1.2 | done |
| KEY-04 | No connection → a "no connection" message, the key is not saved | F1.2 | done |
| KEY-05 | The error text and the app log do not contain the entered key | F1.3, F10.3 | done |
| KEY-06 | A saved key, restart → the key screen does not appear, the session list opens | F10.3 | done |
| KEY-07 | Settings: OpenRouter shows only the last four characters of the key; an unconfigured provider shows "Not connected" | F1.3, S6.4 | done |
| KEY-08 | Settings: replacing the key with an invalid one → an error, the old key keeps working | F1.3 | done |
| KEY-09 | Settings: deleting the OpenRouter key → the provider is "Not connected"; the next time the session list opens — the key screen | F1.3 | done |
| KEY-10 | Any scenario: no requests to addresses outside the fake (self-check of the world) | ADR 0031 | done |
| KEY-11 | The key screen is rotated to landscape and back → the key field stays visible (`@e2e-only`, replaces `FirstRunTests.Rotate_KeepsKeyScreen`) | F10.1 | planned |

### Sessions (`Sessions.feature`, task 255)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| SES-01 | A new session → an empty chat; it is at the top of the session list | S1.1 | done |
| SES-02 | A first message longer than 40 characters → the session title is the first 40 characters | S1.2 | done |
| SES-03 | Renaming: the new title is visible in the list and the chat; the list order does not change | S1.2 | done |
| SES-04 | Renaming to an empty string or to more than 40 characters is not accepted | S1.2 | discrepancy (276) |
| SES-05 | Deleting with confirmation → the session is not in the list, also after a restart; declining the confirmation → the session stays | S1.1 | done |
| SES-06 | A new session gets the default settings: one model `black-forest-labs/flux.2-klein-4b`, one run, the assistant model from Settings | S1.3 | done |
| SES-07 | Changing the default assistant model in Settings affects only new sessions | S1.3 | planned |
| SES-08 | The list row shows the number of runs ("· 2 runs") after two runs | S1.2 | planned |
| SES-09 | Sessions are grouped: today's in "Today", older ones in "Earlier" (time from `FakeTimeProvider`) | S1.1 | done |
| SES-10 | An example from the empty list starts a new session with this text | S1.1 | planned |
| SES-11 | Clearing the whole history from Settings: sessions and files deleted, keys and settings kept | S1.4 | planned (273) |

### Chat and assistant (`ChatAssistant.feature`, task 256)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| CHAT-01 | The assistant answers with text → an assistant message in the feed, no run | S2.2 | done |
| CHAT-02 | The assistant calls `generate_images` → a run block in the feed with the exact prompt of the tool, the card has the same prompt (`@e2e` after 243, replaces `MockSessionTests.FirstRunWithMock_GeneratesImage`) | S2.2, S2.3, F9.4 | done |
| CHAT-03 | The assistant receives the system prompt and the prompt of the last run (the fake checks the request body) | S2.1 | planned |
| CHAT-04 | An assistant error (500, 429, 401) → a separate message with the reason; the user's message is not lost; it can be sent again | S2.5 | planned |
| CHAT-05 | An empty message is not sent (the command is unavailable) | S2.1 | planned |
| CHAT-06 | Retry after an assistant error: the second answer is normal → the run starts | S2.5 | planned |
| CHAT-07 | The session's assistant model is visible in the chat | S1.3 | planned |
| CHAT-08 | A picture attachment goes into the generation as a reference if the model supports them | F4.5 | planned |

### Runs and generations (`Runs.feature`, task 257)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| GEN-01 | Two models × two runs → four cards; the seeds are equal for both models and differ between runs | S3.1, S3.2 | planned |
| GEN-02 | A model without seed → the seed is not sent (request body), the card shows "seed not supported" | S3.3 | planned |
| GEN-03 | One generation fails (500), another is done → the first is "failed" with a short reason, the second "done" | S3.4, S3.5 | planned |
| GEN-04 | The provider filtered → the state is "filtered", not "failed" | S3.5, F6.5, F9.2 | planned |
| GEN-05 | While the provider has not answered → the card is "running"; after the answer — "done" with the picture (`@e2e` after 243) | S3.5 | planned |
| GEN-06 | The picture is saved to a file and the DB before it is shown: the file exists; after a restart the card shows the same picture | S3.6, F7.2 | planned |
| GEN-07 | The card shows the model, seed, price (`usage.cost`) and time | S3.7 | planned |
| GEN-08 | The run summary: number of models, runs, finished ones and the sum of known prices; updated as generations finish | S3.9 | planned |
| GEN-09 | Picture size on the card: `W×H px · ratio` | S3.10 | planned |
| GEN-10 | Changing the session models during a run does not change the run already started | S3.1, S5.2 | planned |
| GEN-11 | The next run estimate in the chat is the sum of the known prices of the selected models | F4.4 | planned |

### Cancellation and recovery (`CancelAndRecovery.feature`, task 258)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| CAN-01 | Cancelling one active generation → "cancelled", the others continue | S3.5.1, F5.4 | planned |
| CAN-02 | Cancelling the whole run → all active ones "cancelled", finished ones do not change | S3.5.1, S3.5.2 | planned |
| CAN-03 | The result arrived before the cancellation → the card stays "done" | S3.5.2 | planned |
| CAN-04 | A generation did not finish, the app was restarted → "Interrupted" (failed) | S3.8 | planned |
| CAN-05 | The time of the cancellation request is stored and visible after a restart | S3.5.3 | planned |

### Card actions (`CardActions.feature`, task 259)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| ACT-01 | Repeat → a new run of one generation: the same prompt, model and seed | S4.1, S4.4 | planned |
| ACT-02 | New seed → the same prompt and model, another seed | S4.2 | planned |
| ACT-03 | Edit → the prompt opens for editing; after confirmation — a run with the edited prompt without the assistant | S4.3 | planned |
| ACT-04 | Other model… on an error → the model picker; "Used in this session" on top; the choice starts a run on the chosen model | S4.5 | planned |
| ACT-05 | Copy prompt → the clipboard holds the exact prompt | S4.6 | planned |
| ACT-06 | Details on an error → the original error text; the card shows a short reason | S4.7, F9.1 | planned |
| ACT-07 | Save image → a save dialog with the name `imagegen-<8 characters of id>.<extension>` | S4.8 | planned |
| ACT-08 | Actions are unavailable where the specification does not give them (Details on a done card, Save on an error) | S4.5–S4.8 | planned |

### Session models and parameters (`SessionModels.feature`, task 260)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| MOD-01 | Choosing two models and three runs → the next run is two models × 3 | S5.1, S5.2 | planned |
| MOD-02 | The number of runs is limited to 1–10 | S5.1 | planned |
| MOD-03 | Search in the model list narrows the list by name and id | S5.1 | planned |
| MOD-04 | Models are grouped by vendor from the id prefix | S5.5 | planned |
| MOD-05 | The price in a model row is only `output_image`; without a price — "price unknown" | S5.6 | planned |
| MOD-06 | Refresh re-requests the catalog and prices; without Refresh reopening uses the cache (no requests) | S5.6 | planned |
| MOD-07 | Cancel does not save the changes | S5.1 | planned |
| MOD-08 | Session parameters (aspect ratio, resolution, format) go into the generation request body | F4.2 | planned |
| MOD-09 | A selected model vanished from the catalog → this is visible and it can be removed | — | after 236 |

### Settings and appearance (`Settings.feature`, task 261)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| SET-01 | The default style is Quiet, the theme is as in the system | S6.1, S6.2 | planned |
| SET-02 | Changing the style and the theme applies immediately and is kept after a restart | S6.3 | planned |
| SET-03 | Settings is entered from the sessions screen; the screen is called "Settings" | S6.4 | planned |
| SET-04 | Clear logs deletes the log files, the app keeps writing the log | — | planned (261) |

### Spending (`Spending.feature`, task 262)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| SPD-01 | Two finished generations with a price → the total for the day is their sum, grouped by provider and model | F8.3 | planned |
| SPD-02 | A generation without a price → counted in "operations with unknown price", not in the sum | F8.3 | planned |
| SPD-03 | The week is ISO, from Monday; a generation of last Sunday is not in the current week | F8.3 | planned |
| SPD-04 | The "expensive run" threshold is off by default; with a threshold below the estimate the assistant warns before sending | F8.4 | planned |
| SPD-05 | The monthly limit is kept after a restart | F8.4 | planned |

### Search (`Search.feature`, task 263)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| SRCH-01 | Words: searching by a session title and by a generation prompt finds the session | S1.4 | planned |
| SRCH-02 | Words: nothing found → the "no results" state; clearing the search returns the list | S1.4 | planned |
| SRCH-03 | Meaning is off by default; turning it on in Settings indexes the history (embeddings from the fake) | S1.4 | planned |
| SRCH-04 | Meaning: results are not mixed with Words | S1.4 | planned |
| SRCH-05 | The index status and the choice of the embedding model | — | planned (263) |

### Gallery, viewer and comparison (`Gallery.feature`, task 264)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| GAL-01 | The gallery shows only successful pictures of all sessions, newest first | F7.3 | planned |
| GAL-02 | Filter by model and period (7 and 30 days) | F7.4 | planned |
| GAL-03 | Opening a picture from the gallery opens its source session | F7.3 | planned |
| GAL-04 | Viewer: paging only within one run | F6.2 | planned |
| GAL-05 | Comparing two successful pictures of one session: side-by-side and overlay modes; the zoom is shared | F6.3 | planned |

### Video (`Video.feature`, task 265)
| id | Scenario | Requirements | Status |
|---|---|---|---|
| VID-01 | Without selected video models the assistant is not offered `generate_video` (request body) | media-generation | planned |
| VID-02 | With a video model: a `generate_video` call → an asynchronous job, polling, the card is "done" with a video file | media-generation | planned |
| VID-03 | A video download error → "failed", no partial file | media-generation | planned |
| VID-04 | Cancelling a video → "cancelled" with a warning that the provider may continue | media-generation | planned |
| VID-05 | Audio: with an audio model the assistant gets `generate_audio`, the card becomes done with an audio file | media-generation | planned (265) |

### Migrated flow tests (task 267)
Task 267 maps every test of `ImageGen.Avalonia.Tests/Flows/*FlowTests.cs` to a scenario above; flow tests without a matching scenario get new rows here (next free id in the area) before they are deleted.

## Acceptance criteria
- Every scenario with the status "planned" is automated or moved to "discrepancy (NNN)".
- `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj` is green on Linux and in CI.
- `@e2e` scenarios are green in `ui-e2e.yml` (task 266).

## Constraints and out of scope
- MAUI markup and platform behavior (Android, Windows, iOS) are not here, except `@e2e`: `Presentation.Tests/Design`, E2E (ADR 0029, 0030), manual 016, 046, 140.
- Live providers are not called. The fake's response shapes follow `OpenRouterFakes` and the client code; discrepancies with real services stay open questions in `docs/status.md`.
- The fal and Together providers — only key scenarios, while the fake cannot do them (task 253 decides whether that is enough).

## Tests
`ImageGen/ImageGen.Acceptance.Tests/Features/*.feature`, bindings in `Steps/`, the world in `Support/`.
