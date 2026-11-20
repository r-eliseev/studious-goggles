# Small-model spec probe: results (2026-11-21)

Protocol and rubric: `protocol.md` (committed in `19f2732` before any run). Raw outputs: `runs/<model>/<spec>-<run>.md`. Per-run scores with the quoted evidence: `scores.csv`. Judge: the assistant (Claude Opus 5.5).

## Hit rate per case (hit = 1, partial = 0.5)

| Case (spec → fix) | Kind | qwen-local, 5 runs | haiku, 5 runs | sonnet (reference), 3 runs |
|---|---|---|---|---|
| `swipe-row-tap` (162 → 214) | platform | 0.40 (0 hits, 4 partial) | **1.00** (5 hits) | **1.00** (3 hits) |
| `autoscroll-tall-row` (097 → 159) | platform + layout | 0.00 | 0.40 (1 hit, 2 partial) | **1.00** (3 hits) |
| `touch-behavior-children` (069 → 211) | platform | 0.10 | 0.70 (2 hits, 3 partial) | **1.00** (3 hits) |
| `sidebar-no-refresh` (067 → 089) | missing sentence | 0.10 | **0.00** | **0.00** |
| `run-header-static` (068 → 096) | mechanism left open | 0.00 | 0.80 (3 hits, 2 partial) | 0.67 (1 hit, 2 partial) |
| `index-status-stuck` (105 → 226) | unstated transitions | 0.10 | 0.40 (0 hits, 4 partial) | 0.50 (0 hits, 3 partial) |
| `file-save-not-terminal` (113 → 144) | spec prescribed the defect | 0.20 | 0.60 (1 hit, 4 partial) | 0.83 (2 hits, 1 partial) |
| **All cases** | | strict 0.00, with partial 0.13 | strict 0.34, with partial 0.56 | strict 0.57, with partial 0.71 |

## Noise
- No model kept to "at most 10 items": haiku and sonnet give 10 to 25 numbered items per run, qwen 7 to 19, on the cases and on the controls alike (a two-line fix, control 182, got 10 to 17 items from haiku). The probe never answers "nothing to ask".
- Sample labelling of the controls (haiku and sonnet run 1 of 182, haiku run 1 of 150): about a third to a half of the items look like valid gaps or risks (for example `ru-RU` missing under invariant globalization on Linux, `decimal` versus `double` sums, test culture across `await`), the rest are answered by the spec, generic, or wrong (haiku: "`dotnet test --project` is not a valid option", which is false for this SDK). None of these items led to rework (the controls had none). So on a control a reader gets about 5 to 7 plausible points per run that turned out not to matter.
- On a case the hitting item is one of 15 to 25: the signal exists but has to be found by a reader who already knows what to look for.

## Cost and time
| Model | Runs | Time | Cost |
|---|---|---|---|
| qwen-local (Qwen2.5 3B Instruct, Ollama 0.12.6 in Docker, 6 CPUs, no GPU) | 55 | 3585 s total, 65 s per run | 0 (electricity) |
| haiku (Claude Haiku 5.5, `claude -p`, no tools) | 55 | 848 s | $0.088 |
| sonnet (Claude Sonnet 5.5, same setup) | 33 | 757 s | $1.07 |

## Conclusion and limits
1. **A local 3B model is not useful as a spec probe here** (no strict hit in 35 runs; its items are generic: "concurrency issues", "platform differences"). The hypothesis that a different family gives useful error diversity cannot be tested with a model this weak: the capability floor dominates (the confound named in `research/formal-specs-and-spec-testing.md`).
2. **A small Claude model finds the platform-behaviour risks in the plan** (162, 069; partly 097) and the "mechanism left open" hole (068), at almost no cost. The expectation written before the run (low hit rate on platform pairs) was wrong for haiku and sonnet: those plans stated a hypothesis about a library and the models doubted it. Caveat: the 162 spec itself had a fallback step and the 097 spec said "the last index does not prove the bottom edge", so the models could build on hints in the text.
3. **The missing sentence (067) was found by no model**, and the unstated transitions (105) only as generic questions about `Failed`, never as "re-enable after Ready" or "key restored". The holes that matter most for a spec-quality factory (what the spec does not mention) are the ones this probe misses.
4. **Repeated runs did not separate signal from noise**: hits appear in most runs of the same case, but so do the plausible non-issues on controls. "Points raised in all five runs" is therefore not a usable filter without more data.
5. Limits: 7 cases and 4 controls; one judge, of the same family as two of the three models and as the authors of the specs (judge bias, correlated errors); hit detection for long outputs started from a keyword filter and then a reading of the matching items, so a hit phrased without the keywords could be missed (a risk mostly for qwen; its outputs for 069 and 162 were read in full); the Claude models may have seen similar MAUI issues in training (contamination by general knowledge is the point of the probe, not a defect); noise labelling is a sample, not every item.

**Recommendation (for the owner's decision):** do not use the small local model. A small Claude model as a probe is cheap enough to try in the harness, but only with a changed protocol: (a) ask for **risks of the plan's library and platform assumptions** and for **what the spec does not mention about state that changes while visible / after a transition**, as two separate narrow prompts instead of one broad list; (b) hard-limit the output (for example 5 items, each with the spec line it is about) and reject longer outputs; (c) measure again on new cases before it becomes a mandatory step. The missing-sentence class needs another mechanism (for example a checklist of state changes per screen, or acceptance scenarios written by a different agent from the spec only).
