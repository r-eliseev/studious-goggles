# Specification: a multi-provider client for image generation

Status: draft
Code: the v0.1 skeleton in `ImageGen/` (`docs/architecture.md`)
Platform goals and decisions added after the specification: `docs/product.md`.

Oct 27, 2026 · @Roman

## Goal and scope

The product is a client for image generation: the keys of the built-in providers fal.ai, OpenRouter and Together AI are connected once; Together is available only for images and its live response has not been verified yet. Other OpenAI-compatible providers are added through the config. One prompt goes to several models at once, the results appear side by side with price and time. The document describes the functionality and the UI.

**Principles**

- Own keys (BYOK): the app does not sell generations and does not stand between the user and the provider in money matters.
- One prompt, many models: a parallel run is the main scenario, not an add-on over a single generation.
- Common parameters: aspect ratio, number of pictures and seed are set once; the app translates them into each provider's parameters and shows what a model does not support.
- The result is never lost: provider links expire (fal keeps the file for a configurable time), so the result is saved on the user's side right after it is ready.
- Transparency: every run shows the provider, model, parameters, time and cost. If the provider filtered a picture, this is shown explicitly, not as an empty frame.
- Provider moderation is not bypassed: the app offers only the settings the provider itself documents (for example, the safety checker switch at fal).

**In the scope of the first version:** generation from text, editing from reference pictures, parallel run and comparison, gallery, spending tracking.

**Out of the scope of the first versions:** video and audio (generation of music, sounds and video is planned for later versions: ADR 0027, tasks 199–209; the behavior specification comes with the implementation), LoRA training, a canvas editor with masks, multi-user mode.

## User scenarios

The main scenario: one prompt, several models, results side by side. The others serve it.

1. **First setup.** The user enters the keys of the built-in providers fal, OpenRouter and Together AI; Together provides only image models and does not take part in choosing the assistant model. The user adds other providers through the config. The app checks the keys and shows the model catalog.
2. **One prompt for several models.** The user chooses a model set (for example "Photorealism"), writes a prompt and presses "Run". Results appear as they are ready; each shows the model, time and price.
3. **Comparison.** The user opens two results side by side, zooms into a detail, marks the best one and adds it to favorites.
4. **Refinement.** The best result becomes a reference, the prompt changes, the run goes to two models of the set.
5. **Refusal or filter.** One model returned a black frame or an error. The user sees the reason and with one tap runs the same prompt on another model.
6. **Repeat with a change.** The user opens a past run from the history, changes one parameter (for example the seed) and runs again.
7. **Spending control.** The user looks at the month's spending by provider and model and sets a monthly limit.
8. **A new model.** The user finds a fresh model in the catalog, tries it on the same prompt and adds it to their set.

## Functional requirements

Priorities: **P0** — needed for the first version, **P1** — the next round, **P2** — later.

### 1. Provider connections

- **F1.1 (P0)** A key is entered for each of the three providers separately; any subset can be connected.
- **F1.2 (P0)** On saving, the key is checked; the status is always visible: "works", "invalid key", "no connection".
- **F1.3 (P0)** A saved key is never shown whole, only its end is visible. The key can be replaced or deleted.
- **F1.4 (P1)** Several connections to one provider (for example a personal and a work key), each with its own name.
- **F1.5 (P2)** The balance or remaining funds, if the provider returns it through the API.

### 2. Model catalog

- **F2.1 (P0)** One list of the models of all connected providers: name, provider, price estimate per run, number of references, a license mark if known (for example "non-commercial").
- **F2.2 (P0)** Search and filters: provider, price, reference support, output format, the ability to return text together with the picture.
- **F2.3 (P0)** A model card: description, supported parameters with allowed values, price with a unit (per picture, per megapixel, per tokens), limits (pictures per request, references, seed availability).
- **F2.4 (P1)** The OpenRouter list is refreshed from its API automatically; new models get a "new" label.
- **F2.5 (P1)** A fal model is added by hand by its identifier: the app reads the model's input schema and builds the parameter fields. For an OpenAI-compatible provider the models are listed in the config (`imageModels`), without reading a schema.
- **F2.6 (P1)** The same model at different providers (for example FLUX) is shown as variants with different prices.
- **F2.7 (P1)** Favorite and hidden models.

### 3. Model sets and presets

- **F3.1 (P0)** A named model set for a simultaneous run, chosen with one tap.
- **F3.2 (P0)** Quick selection with checkboxes in the catalog without creating a set.
- **F3.3 (P1)** A preset of common parameters (aspect ratio, number of pictures, quality, format) separate from the set.
- **F3.4 (P1)** A model inside a set can have its own values (for example high quality for an OpenAI model).
- **F3.5 (P2)** Starter sets ("Photorealism", "Quick drafts", "Text in the picture") assembled from the catalog.

### 4. Request input

- **F4.1 (P0)** The prompt field. A negative prompt is shown only if at least one selected model accepts it.
- **F4.2 (P0)** Common parameters for all models: aspect ratio, resolution, number of pictures per model, seed, format, quality.
- **F4.3 (P0)** For each selected model it is visible which common parameters it applies, replaces or ignores. If a model accepts one picture per request, the app makes several requests itself.
- **F4.4 (P0)** A cost estimate before the run: the sum over models and the number of runs; the unknown part is marked separately, an estimate by megapixels is marked "≈".
- **F4.5 (P1)** Reference pictures: upload, paste, link. Models that cannot take that many references (limits from 1 to 16) are marked, the app offers to remove the extra ones.
- **F4.6 (P1)** Advanced provider parameters (for example prompt enhancement at fal, provider choice at OpenRouter) in a collapsible block.
- **F4.7 (P1)** A safety checker switch where the provider documents it. By default the provider's behavior applies, with an explanation next to it that for some models the filter cannot be turned off.
- **F4.8 (P2)** Prompt improvement on request, with a "before and after" view; the user decides whether to apply it.
- **F4.9 (P0)** Img2Text: from a finished picture (uploaded, from the gallery or a result) a text description or a prompt is created. The result goes into the prompt field only by the user's action; the provider and model of the description are visible, the cost is accounted as for an ordinary job.

### 5. Runs and jobs

- **F5.1 (P0)** "Run" creates a run group: one job per model.
- **F5.2 (P0)** Jobs run in parallel and independently; a failure of one does not affect the others.
- **F5.3 (P0)** Job states: queued, running, done, failed, cancelled, filtered. The queue position is shown where the provider returns it.
- **F5.4 (P0)** Cancelling one job and the whole group. If the provider does not guarantee stopping, the status is "cancellation request sent".
- **F5.5 (P0)** Repeating a job: the same request, with another seed, on another model.
- **F5.6 (P1)** If a job does not answer for a long time, the app offers to wait or cancel. The threshold is configurable.
- **F5.7 (P1)** A limit of concurrent jobs per provider, so as not to hit the rate limits.
- **F5.8 (P1)** A notification when a group finishes while the app is minimized.
- **F5.9 (P2)** A lower priority for background runs if the provider supports it.

### 6. Results and comparison

- **F6.1 (P0)** A result grid: a card per job with a thumbnail, model, time, price and status. The card comes alive as soon as the result is ready.
- **F6.2 (P0)** Viewing a result in a modal window over the whole client area; the original size is fitted without cropping. The successful results of the same group can be paged through. Native fullscreen: Avalonia and MAUI Android (task 137), MAUI Windows (139); iOS/Mac Catalyst stay unverified.
- **F6.3 (P0)** Comparing two successful results of one session: open from the card menu and choose the second picture with a caption of model, seed and price. Show side by side or overlaid with a slider; zoom and offset are synchronized and normalized to the viewport. Keep the original proportions. If the session or the file is already deleted, show a message and close the comparison; after closing, release the loaded images.
- **F6.4 (P0)** Actions on a result: download, copy, share, add to favorites, use as a reference, repeat, delete.
- **F6.5 (P0)** A result filtered by the provider is shown as a separate card with an explanation and a "run on another model" button.
- **F6.6 (P1)** A "best in group" mark and a rating. The accumulated marks give a summary: which model wins more often on your prompts.
- **F6.7 (P1)** The full run parameters: what was actually sent to the provider and which seed it returned.
- **F6.8 (P2)** A prompt series: a table where rows are prompts and columns are models.

### 7. Gallery and history

- **F7.1 (P0)** All runs are saved: picture, prompt, parameters, model, provider, time, price.
- **F7.2 (P0)** The picture is downloaded and saved as soon as it is ready: fal keeps the file for a configurable time, OpenRouter and OpenAI-compatible providers return the picture only in the response.
- **F7.3 (P0)** A history feed by groups. Opening a group brings back the composer with the same prompt, parameters and set; the gallery of all saved successful images opens the source session.
- **F7.4 (P1)** Gallery filters by model and period (all time, the last 7 or 30 calendar days). Full-text search, a provider filter separate from the model, favorites and ratings are not implemented yet.
- **F7.5 (P1)** Tags and collections.
- **F7.6 (P1)** Export and backup: pictures and a file with run descriptions.
- **F7.7 (P1)** Deleting with confirmation. An independent ledger keeps the paid operations after a picture or session is deleted; an unknown price is not counted as zero.
- **F7.8 (P2)** Synchronization between devices (depends on the platform choice).

### 8. Spending

- **F8.1 (P0)** The cost of a job: an estimate before the run, the actual amount after (if the provider returns it, as OpenRouter does through usage.cost), otherwise the price-list price marked "≈". A repeated write updates the same operation; a zero price is stored separately from an unknown one.
- **F8.2 (P0)** The total of a run group.
- **F8.3 (P1)** The spending screen: for the local day, the ISO week (from Monday), the local month, by provider and model; the number of operations with an unknown price in existing sessions is shown separately.
- **F8.4 (P1)** A monthly limit and a threshold "warn if a run costs more than X". The threshold is off by default; a manual run requires confirmation, and for the assistant the warning is shown before the message is sent, without forbidding it.
- **F8.5 (P2)** The price per successful result: the model's spending divided by the number of "best" marks.

### 9. Errors and moderation

- **F9.1 (P0)** Errors are explained clearly, with the provider's original text. Distinguished: invalid key, no funds, rate limit, model unavailable, rejected by moderation, timeout, network failure.
- **F9.2 (P0)** Filtering by the provider (a black frame at fal, a refusal at others) is a separate state, not a failure and not an empty picture.
- **F9.3 (P1)** Automatic retry on temporary errors with a limited number of attempts and within the spending limit.
- **F9.4 (P0)** The app does not rewrite the prompt in secret and does not substitute the model without the user's knowledge.

### 10. Settings, security and privacy

- **F10.1 (P0)** A light and a dark theme. The UI language is English only (the owner's decision of 2026-10-31, task 028 cancelled); generation prompts are in English; the assistant answers in the language the user writes in.
- **F10.2 (P0)** The result storage location.
- **F10.3 (P0)** Keys are stored in the platform's secure store and never get into logs and exports.
- **F10.4 (P1)** Default parameters, file format, the file name template.
- **F10.5 (P1)** A hashed user identifier for the user parameter at fal and OpenRouter.
- **F10.6 (P1)** A clear warning: prompts and reference pictures go to the provider.

## UI: screens and mockups

Five sections: **Studio** (run and results), **Gallery** (history), **Models** (catalog and sets), **Spending**, **Settings** (connections and default parameters). On a wide screen the sections are in the top or side bar, on a narrow one in bottom tabs, and the composer rises from the bottom of the screen. The mockups are schematic: prices and values are illustrative, model limits are taken from the [OpenRouter catalog](https://openrouter.ai/blog/insights/image-generation-models-compared/) as of 11.09.2026.

### Studio (wide screen)

The main screen: request input on top, the result grid of the current run group below.

```text
+--------------------------------------------------------------------------+
| Studio  Gallery  Models  Spending  Settings          fal OK  Repl OK  OR OK |
+--------------------------------------------------------------------------+
| Prompt                                                                   |
| +----------------------------------------------------------------------+ |
| | Portrait of a man by a window, cigarette smoke in a ray, 35 mm, grain| |
| +----------------------------------------------------------------------+ |
| References: [+]    Set: [Photorealism v] 4 models   Params: 1:1 1K x1    |
| Estimate: ~ $0.17                                       [     Run     ]  |
+--------------------------------------------------------------------------+
| Group 14:52   3 of 4 done   $0.17                       [Cancel all]     |
| +-------------+ +-------------+ +-------------+ +-------------+          |
| |             | |             | | running...  | | ! filtered  |          |
| |   picture   | |   picture   | | queue: 2    | | by the      |          |
| |             | |             | |             | | provider    |          |
| +-------------+ +-------------+ +-------------+ +-------------+          |
| | FLUX.2 Klein| | Qwen Image 3| | Seedream 5  | | Grok Imagine|          |
| | OpenRouter  | | OpenRouter  | | Lite, fal   | | OpenRouter  |          |
| | 5.8 s $0.009| | 13 s  $0.03 | | ...         | | 3.6 s $0.05 |          |
| | * v ~ ...   | | * v ~ ...   | | [Cancel]    | | [Other model]|         |
| +-------------+ +-------------+ +-------------+ +-------------+          |
+--------------------------------------------------------------------------+
```

Legend: `*` best in group, `v` download, `~` repeat, `...` other actions. Cards take their place right at the run, so that the grid does not reflow when results arrive. The card order matches the order in which the models were selected.

### Job card states

| State | What the card shows | Available actions |
| --- | --- | --- |
| Queued | A placeholder, the queue position if the provider returns it | Cancel |
| Running | A placeholder with an indicator, provider logs on tap | Cancel |
| Done | The picture, model, provider, time, price | View, download, favorite, best, reference, repeat, delete |
| Failed | A short reason in plain language, the original text on tap | Repeat, on another model |
| Cancelled | A gray placeholder, the mark "cancellation request sent" if stopping is not guaranteed | Repeat |
| Filtered | The mark "filtered by the provider", an explanation that this is the provider's decision | On another model, open the model's moderation settings (if any) |

### Choosing models

Opens as a panel or a separate screen from the composer and from the "Models" section.

```text
+--------------------------------------------------------------------------+
| Models                                                           [Done]  |
| Search [ flux_______ ]  Provider [All v]  [ ] with references  Price [Any v] |
+--------------------------------------------------------------------------+
| Set: [Photorealism v]  [Save as a set]              selected: 4  ~ $0.17 |
+--------------------------------------------------------------------------+
| [x] FLUX.2 Klein         OpenRouter  $0.014 / picture    references: 4   |
| [x] Qwen Image 3         OpenRouter  $0.03  / picture    references: 4   |
| [x] Seedream 5.0 Lite    OpenRouter  $0.035 / picture    references: 14  |
|     also at: fal.ai, Together AI (variants with another price)          |
| [x] Grok Imagine 2.0     OpenRouter  $0.06  / picture    references: 3   |
| [ ] FLUX.2 Pro           OpenRouter  $0.03  / megapixel  references: 8   |
| [ ] GPT Image 2          OpenRouter  from $0.006 (tokens) references: 16 |
+--------------------------------------------------------------------------+
| [+ Add a fal model by identifier]                                       |
+--------------------------------------------------------------------------+
```

Marks in a row: "new", "open model", "license: non-commercial", "the filter cannot be turned off". A row expands into a model card with parameters, price and limits.

### Applying parameters to models

One parameter set in the Studio is applied to each model in its own way. Before the run the app shows what will be passed, what is replaced with the nearest allowed value and what is dropped. The user sees this before spending money, not after.

| Parameter in the Studio | How it is applied | If the model does not support it |
| --- | --- | --- |
| Aspect ratio | Passed as is | The nearest allowed one, the card shows "Aspect ratio: 3:2 → 4:3" (a replacement; if 1:1 is allowed, there is no replacement) |
| Size / resolution | A preset or width × height, whichever the model accepts | The nearest preset, a mark on the card |
| Number of pictures | One request for N pictures if the model can; otherwise N requests | Separate requests, the price is multiplied |
| Seed | Passed if the model accepts it | The parameter is dropped, the card shows "seed not supported" |
| Reference pictures | Passed up to the model's limit (from 1 to 16 for different models) | The model is marked "no references" and skipped if references are required |
| Safety filter | The setting is on/off where the provider allows it | The provider's filter stays, the card shows "cannot be turned off" |
| File format | PNG, JPEG or WebP | The model's default format |

```text
+------------------------------------------------------------------------+
| Check before the run                                           [Back]  |
+------------------------------------------------------------------------+
| FLUX.2 Klein      OK: all parameters applied                           |
| Qwen Image 3      seed passed, 4 references of 5 (the model's limit)   |
| Seedream Lite     aspect ratio 3:2 replaced with 4:3                   |
| Grok Imagine      seed not supported and dropped                       |
+------------------------------------------------------------------------+
| Estimate: ~ $0.14                                  [     Run     ]     |
+------------------------------------------------------------------------+
```

### Comparing results

Two results open side by side, on top of each other with a slider, or overlaid. Zoom and panning are synchronized. Under each picture are the time, price and seed, so that the comparison is fair. The "Best" button remembers the choice in the group and is shown in the Gallery.

```text
+------------------------------------------------------------------------+
| Comparison                                                     [Close] |
| A: FLUX.2 Klein (OpenRouter)          B: Qwen Image 3 (OpenRouter)     |
| +------------------------------+ +------------------------------+      |
| |                              | |                              |      |
| |          picture A           | |          picture B           |      |
| |                              | |                              |      |
| +------------------------------+ +------------------------------+      |
| Mode: (o) side by side ( ) slider ( ) overlay  Zoom 100% [x] sync      |
| A: 5.8 s  $0.014  seed 424242        B: 13 s  $0.03  seed 424242       |
| Prompt: Portrait of a man by a window...         [Best: A] [Best: B]   |
+------------------------------------------------------------------------+
```

### Gallery

Each run group is shown as a row of thumbnails with the prompt, the number of models, the best-result mark and the total price. Search goes over prompts. Filters: model, provider, favorites, tag. From a group you can repeat the run with the same parameters or open any result for comparison.

```text
+------------------------------------------------------------------------+
| Search [_______] Model [All v] Provider [All v] [ ] favorites          |
+------------------------------------------------------------------------+
| Today                                                                  |
| +---+ +---+ +---+ +---+                                                |
| |   | |   | |   | |   |   Portrait of a man by a window, smoke...      |
| |   | |   | |   | |   |   4 models, best: Qwen Image 3, $0.14          |
| +---+ +---+ +---+ +---+   [Open] [Repeat] [Delete]                     |
|                                                                        |
| Yesterday                                                              |
| +---+ +---+ +---+ +---+                                                |
| |   | |   | |   | |   |   A neon sign at night, wet asphalt...         |
| |   | |   | |   | |   |   3 models, best: not marked, $0.09            |
| +---+ +---+ +---+ +---+   [Open] [Repeat] [Delete]                     |
|                                                                        |
+------------------------------------------------------------------------+
```

### Connections

Keys are entered once. After entering, the app makes a test request and shows the status. The key is visible only by its last four characters. The warning about where prompts and references go is shown on the same screen.

```text
+------------------------------------------------------------------------+
| Settings: Connections                                                  |
+------------------------------------------------------------------------+
| fal.ai       key: ........a1f3   works            [Replace] [Delete]   |
| Together AI  key: ........9c20   invalid key      [Replace] [Delete]   |
| OpenRouter   not connected                        [Connect]            |
| [+ Add another connection]                                             |
+------------------------------------------------------------------------+
| [x] Send a hashed user identifier to the providers                     |
| Prompts and reference pictures are sent to the chosen providers.       |
+------------------------------------------------------------------------+
```

### Spending

Spending is counted from the actual amount if the provider returned a price (OpenRouter gives it in the response), otherwise from the price-list estimate marked "estimate". The monthly limit warns before a run when the group's estimate exceeds the remainder.

```text
+------------------------------------------------------------------------+
| Spending   Period [October 2026 v]            Limit: $20.00 [Change]   |
+------------------------------------------------------------------------+
| Total: $4.83 of $20.00   [#####....................]  24%              |
|                                                                        |
| By provider                    By model                                |
| OpenRouter  $3.10  ########    FLUX.2 Klein      $0.41  ##             |
| fal.ai      $1.45  ####        Qwen Image 3      $1.20  #####          |
| Together AI $0.28  #           Grok Imagine 2.0  $1.62  ######         |
|                                                                        |
| Most expensive run: Seedream 5.0 Pro, $0.09 per picture                |
+------------------------------------------------------------------------+
```

### Narrow screen (phone)

On a narrow screen the Studio elements go one under another: the prompt, the model set, the run button, then the cards in two columns. Sections are the bottom navigation. All the same functions are available, only the layout changes.

```text
+------------------------+
| Studio              [=]|
+------------------------+
| Prompt                 |
| [Portrait of a man by  |
|  a window, smoke..]    |
|                        |
| Set: Photorealism      |
| 4 models   ~ $0.14     |
| [        Run        ]  |
+------------------------+
| Group 14:52            |
| 2 done, 1 running,     |
| 1 filtered             |
| +------+ +------+      |
| | img  | | img  |      |
| |Klein | |Qwen  |      |
| +------+ +------+      |
| +------+ +------+      |
| | run. | |! filt|      |
| |Seedr.| |Grok  |      |
| +------+ +------+      |
+------------------------+
| Stud  Gal  Mod  Spend  |
+------------------------+
```

### General UI rules

- Result cards take their place right after the run; the grid does not jump when pictures arrive.
- An error of one model does not block the others; its card has the provider's reason text and a "Repeat" or "Other model" button.
- A filtered result (a black frame or a refusal) is marked as a separate state, not an ordinary error, so that it is visible that the provider is responsible, not a failure.
- The price estimate is always visible before the run, the actual price after.
- The result is saved locally as soon as it is received, because fal keeps the file for a configurable time, and OpenRouter and OpenAI-compatible providers return the picture in the response.
- Status texts are duplicated by icons and do not depend on color alone.

## Data entities

A conceptual model: what the app stores and how it is related, without tying it to a storage method.

| Entity | What it is | Main fields |
| --- | --- | --- |
| Connection | Access to one provider | provider, key (hidden), check status, check date |
| Catalog model | A model available through a connection | identifier, provider, name, supported parameters, reference limit, unit and price, marks (new, open, license, filter) |
| Model set | A named list of models | name, models, order |
| Parameter preset | Saved settings | name, aspect ratio, size, number, format, seed |
| Reference | An uploaded input picture | file, name, tags, where it was used |
| Run group | One press of "Run" | prompt, parameter set, model list, time, estimate and final price, best result |
| Job | The run of one model in a group | model, connection, state, applied and dropped parameters, time, error |
| Result | A received picture | file, dimensions, seed, actual price, the "filtered" flag, favorite |
| Budget | A spending limit | period, amount, warning threshold |
| Tag and collection | Manual organization of results | name, list of results |

## Constraints, risks and open questions

The main product risk is the differences between providers: one prompt and one parameter set produce requests of unequal accuracy, prices and retention times. The UI must show this, not hide it.

| Constraint or risk | What happens | How the product answers |
| --- | --- | --- |
| Different parameter sets | seed, sizes and the number of references differ between models | The check before the run (section 6.4) |
| Result links expire | fal keeps the file per its setting; OpenRouter and OpenAI-compatible providers return the picture in the response | Saving the file as soon as it is received |
| Different price units | Per picture, per megapixel, per tokens; the declared price may differ from the charged one | An estimate before the run and the actual amount after, separately |
| Cancellation does not always work | fal: a request already being generated may run to the end | The status "cancellation requested" instead of "cancelled" |
| Rate limits | each provider has its own limits (fal has a queue) | A run queue and a clear message on refusal |
| Different moderation | At OpenRouter the model provider's filter stays; at fal the filter cannot be turned off everywhere, a black frame means the filter fired | A separate "filtered" state, the mark "cannot be turned off" |
| Webhooks need a public address | A client without a server cannot receive them | Status polling or an event stream |
| Model licenses | For example, Qwen-Image-2.1 is non-commercial only | A license mark in the model list |
| Provider policies | Service rules and laws always apply | The app does not bypass provider filters, it only shows them honestly and offers another model |

## Open questions

Current product questions, including parameters and checking the real APIs, are kept in `docs/status.md`. Platform and product decisions are recorded in `docs/product.md`.
