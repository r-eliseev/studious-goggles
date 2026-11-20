# Small-model spec probe: protocol and rubric (fixed before the runs)

Task 299. Written 2026-11-20, **before any model run**. Owner's decisions of 2026-11-20 (in the chat): run **both** options (a local small model of another family and a small Claude model); the assistant pre-writes the hit rules and disputed ones go to the owner ("ну давай").

## Question
Does a cheap model, run several times on a task spec and asked for assumptions, questions and risks (not for an implementation), flag the hole that later became a defect? How much noise does it produce on specs that had no later rework?

## Inputs
`inputs/NNN.md`: the task text **as the executor received it** (the version at the executor's claim commit), with the front matter and everything from the "Report" section on removed. The text is in Russian (tasks before 269 were written in Russian); this folder is therefore subject to `english-before-merge`.

Contamination check (done): no input mentions the content of the later fix task; some name later task numbers only in dependency lines (097 names 096; 101 names 089 and 088), without their content. The models have no repository access.

### Cases (the defect came later)

| Case | Spec (input) | Later fix | Known cause (from the fix task) | Kind |
|---|---|---|---|---|
| `swipe-row-tap` | 162 (claim `cd5709a`) | 214 | On Android a tap on the row still opened the `SwipeView` items although the plan put a `TapGestureRecognizer` on the row and set `Threshold`; the fix removed `SwipeView` on phones | platform behaviour of a library |
| `autoscroll-tall-row` | 097 (claim `516677a`) | 159 | One timeline row is a whole run, taller than the viewport; `ScrollTo(last, End)` on Android shows the top of that row, and a scroll-down gesture (last row always visible) plus card updates re-trigger the programmatic scroll, so the user cannot scroll within the run | platform behaviour + an unconsidered layout case |
| `touch-behavior-children` | 069 (claim at `eeca3b1`) | 211 (and 088 on Windows) | `TouchBehavior` on the card's outer `Border` makes the children (image button, "…") input-transparent by default, and behaviours do not inherit `BindingContext`, so `LongPressCommand` binds to null | platform behaviour of a library |
| `sidebar-no-refresh` | 067 (claim at `eeca3b1`) | 089 | The sessions list loads only in `OnAppearing`; in the wide window it stays beside the chat and never re-appears, so new titles, run counts and renames are not shown | missing sentence (unstated rule) |
| `run-header-static` | 068 (claim `fa5702c`) | 096 | `HeaderText` is computed once in the constructor; it does not update when the cards change status and cost. The spec said it updates but left the mechanism open ("subscription or recount") and listed only a static test | mechanism left open, no test for the update |
| `index-status-stuck` | 105 (claim `0437b07`) | 226 | Status state machine: after `Ready`, `EnableAsync` does not restart indexing and counters are not recounted; after the key is removed and restored the status stays `Unavailable/missing_key` | unstated state transitions |
| `file-save-not-terminal` | 113 (claim `7ac2056`) | 144 | An exception from `IImageFileStore.SaveAsync` after a successful provider result only reaches the supervisor (logged); the generation stays Running/Queued in the database. The spec itself prescribed "file Save exception observed by supervisor, no second terminal persistence" | the spec prescribed the defect |

### Controls (no later rework on the topic found by a title scan of `tasks/DONE.md` and the active tasks)
101 (session search: typo in an incomplete word), 150 (whitespace in base64 image limits), 182 (spending total in dollars), 190 (delete orphaned temporary files on startup).

## Rubric (what counts as "the probe flagged the cause")
A run is scored per case: **hit**, **partial** or **miss**. Restating text that is already in the spec is not a hit unless it is raised as a doubt, a question or a risk.

| Case | Hit | Partial |
|---|---|---|
| `swipe-row-tap` | doubts that a tap gesture inside `SwipeView` (with `Threshold`) will stop the swipe items from opening on Android, or asks to verify on a device before choosing, or proposes removing `SwipeView` on phones as the main path | a generic "gesture conflicts on Android" without naming swipe versus tap |
| `autoscroll-tall-row` | names a row taller than the viewport (a run block) and `ScrollTo(..., End)` not reaching its bottom or jumping, **or** says that the user's scroll-down or card updates re-trigger the programmatic scroll and stop the user from scrolling inside the last row | a generic "auto-scroll may fight the user" |
| `touch-behavior-children` | says `TouchBehavior` (or the long-press handler) on the card container may intercept or block taps of the child buttons, **or** that the behaviour's command binding may lack the `BindingContext` | a generic "long press and tap may conflict" |
| `sidebar-no-refresh` | asks whether / says that the side list must refresh when the chat beside it changes (title, runs, rename), or that a list loaded on appearing goes stale when both are visible | a generic "keep the two panes in sync" without the staleness point |
| `run-header-static` | says the update mechanism of `HeaderText` is undecided or untested (only a static test), or asks how the header learns about later card changes | asks which of the two mechanisms to use without noting the risk |
| `index-status-stuck` | names a status transition that is not specified: re-enable after Ready (or after a finished pass), or recovery after the key is restored / the network returns | a generic "status transitions are underspecified" |
| `file-save-not-terminal` | says a file-save failure after a successful provider result leaves the generation without a terminal state, or questions "observed by supervisor, no terminal persistence" | a generic "what if the file save fails" without the terminal-state point |

Case-level result: the share of hits over the runs (partial counted as half, reported separately).

### Noise and false alarms
Every raised point in every run (cases and controls) is labelled by the judge as **valid** (a real gap or risk in the spec), **answered** (the spec already answers it) or **noise** (wrong, irrelevant, generic). On controls a **false alarm** is a point raised as a risk or question that the judge labels valid-looking but which did not lead to rework; reported as points per run and the share of runs with at least one such point. Judge: the assistant (Claude); judge bias is a stated limit; disputed labels go to the owner.

## Probe
- Prompt (system): fixed text in `prompt.txt`; it asks for at most 10 items in three groups (assumptions, questions, risks), most important first, in English, no code, no implementation.
- User message: the input file verbatim.
- Runs: **5 per spec** with sampling, for each small model.
- Models:
  - `qwen-local`: Qwen2.5 3B Instruct (another family than Claude), Ollama in Docker on the owner's machine, CPU only, temperature 0.8, seeds 1 to 5, context 16384.
  - `haiku`: Claude Haiku 5.5 through `claude -p` with no tools, a replaced system prompt, no session persistence, run in an empty directory outside the repository (checked: the model reports no files, no project instructions, no memory, no tools). Same family as the judge and as the authors of the specs: correlated errors are a stated limit.
  - Reference: Claude Sonnet 5.5, same setup, 3 runs per spec.
- Recorded per run: raw output (`runs/<model>/<NNN>-<run>.md`), time, tokens and cost where available.
