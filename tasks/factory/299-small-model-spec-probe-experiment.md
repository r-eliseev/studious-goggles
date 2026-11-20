---
id: 299
status: review
assignee:
claimed:
created: 2026-11-18
---

# Experiment: does a small model, run several times on a spec, catch the holes that later became defects

## Description
Owner's decision (2026-11-18): search the history for past ImageGen defects connected with an unclear spec and run the experiment (`small-model-spec-probe` in `docs/factory/open-threads.md`; background: `docs/factory/research/formal-specs-and-spec-testing.md`, section 2).

Hypothesis (from research, not verified): running a cheap model several times on a task spec and asking for assumptions and questions (not the implementation) exposes under-specified places, which can then be fixed before the large model runs.

Candidate cases found in history (task texts: `git show <commit>^:<path>`, paths in `tasks/DONE.md`; titles are in `tasks/DONE.md`, texts are in Russian until task 269):
- Pair A: 162 (Android session row tap; fix did not work on the device) then 214.
- Pair B: 088 (Windows, `TouchBehavior` captures the pointer) then 211 (same toolkit default on Android; behaviours do not inherit `BindingContext`).
- Pair C: 097 (chat timeline does not scroll, MAUI) then 159 (Android, within a run). To verify that 159 is a recurrence.
- Pair D: 089 and 096 (lists and summaries do not update). To verify.
- Others to review: 226 after 215 (semantic index status stuck), 144 (image save failure must fail the job).
In pairs A to C the missing knowledge was **platform behaviour of a library**, not an ambiguity in the text. The expected result, written before the experiment: a low hit rate on these pairs. A low result is a valid and useful outcome. Also look for cases where the cause was a missing sentence in the spec (an unstated rule, an edge case).

Depends on: none (task 298 shares the history search).
Environment: this sandbox has 4 CPUs, 15 GB RAM and reaches huggingface.co (checked 2026-11-18); a CPU-only small model of about 3 billion parameters is feasible but slow. A small model of another family than Claude is preferred (correlated errors). A cheaper fallback: a small Claude model through the sandbox, noting that it is the same family.

## Acceptance criteria
- At least six cases are selected and listed with links: for each, the spec as it was before the defect (task text at the commit before the fix), the later task that fixed it, and the known cause, written down **before** any run (the rubric: what counts as "the probe flagged the cause").
- At least four control specs (tasks that had no later rework on the same topic) to measure false alarms.
- The probe protocol is fixed before the runs: N runs per spec (suggested 5) with sampling, the prompt asks for assumptions, questions and platform risks, not for an implementation; the model gets only the spec text (no repository access, no later tasks, no knowledge files).
- Results table: per case hit or miss (by the pre-written rubric; disputed items go to the owner), false-alarm rate on controls, how many raised points were noise, cost and time. A larger model is run on the same input as a reference.
- The report states the conclusion with its limits (small sample, judge bias, contamination checks) and a recommendation: use, do not use, or change the protocol.

## Analysis

## Plan

## Report
Done 2026-11-20 to 2026-11-21 (owner, 2026-11-20: run both a local small model and a small Claude model; the assistant pre-writes the hit rules, disputed ones go to the owner).
- Protocol, rubric and inputs committed before any run: `docs/factory/experiments/small-model-spec-probe/protocol.md` (`19f2732`). Seven cases (162, 097, 069, 067, 068, 105, 113 with fixes 214, 159, 211, 089, 096, 226, 144) and four controls (101, 150, 182, 190); inputs are the task texts at the executor's claim commit.
- Runs: Qwen2.5 3B (Ollama in Docker, CPU) 5 per spec; Claude Haiku 5.5 5 per spec; Claude Sonnet 5.5 (reference) 3 per spec; all without tools or repository access. Raw outputs in `runs/`, scores with quotes in `scores.csv`, summary in `results.md`.
- Result (hit = 1, partial = 0.5): qwen 0.13, haiku 0.56, sonnet 0.71. Platform-assumption risks found by haiku and sonnet (162, 069, partly 097); the missing sentence (067) found by no model; unstated transitions (105) only as generic questions. Noise: 10 to 25 items per run on every spec, including two-line controls.
- Recommendation: do not use the local 3B model; a small Claude model only with a changed protocol (two narrow prompts, hard output limit, new measurement). Details and limits in `results.md`.
- Disputed score for the owner: haiku 097 run 5 (counted as hit).
- Cost: haiku $0.088, sonnet $1.07, qwen CPU time about 60 minutes.

## Review

## Comments

### 2026-11-18, planner-session
Created on the owner's instruction ("yes, let's search and run the experiment"). The history search is done at title level; the choice between a local small model and a small model of the same family is open and was put to the owner.
