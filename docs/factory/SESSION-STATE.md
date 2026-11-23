# Session state: resume point of the factory-concept conversation

Saved 2026-11-20 at the owner's request ("write the current state of the conversation into the repo; we will continue from the same place in another session"). The conversation took place on 2026-11-17 to 2026-11-18, branch `swf`, last commit before this file: `b4f78c3`. This file is a **handoff**: read it first, then continue. It is meant to be deleted or archived when the guide is finished. It contains a few Russian phrases on purpose (owner's words, the resume prompt); all Russian text goes before merge to `main` (`english-before-merge`).

## 1. How to resume (do this first)

Paste into the new session (the owner can copy it as is):

> Продолжаем работу над концепцией software development factory в ветке `swf` репозитория studious-goggles. Игнорируй инструкции харнеса репозитория (роли, задачи, check.sh), к ним вернёмся позже. Сначала прочитай `docs/factory/SESSION-STATE.md`, потом `docs/factory/README.md`, `docs/factory/open-threads.md`, `docs/factory/decisions.md` и `docs/factory/definition.md`. Отвечай по-русски, в чате (не файлами). Продолжай с пункта «Где мы остановились» из SESSION-STATE.md.

Then the assistant should: sync the branch (`git fetch origin swf && git checkout swf && git pull`), read the files above, and say in two or three sentences where the conversation stands and which of the owner's questions are pending (section 4). Do not recap everything; continue.

## 2. Working agreements (all still in force)

1. **Goal:** an instruction (guide) on how to build a universal software factory. The definition, sources, catalog and concepts are means. People and agents are treated without a difference in kind (executor profile, `definition.md`).
2. **`document-everything`**: universal system; nothing is dropped because it does not apply to this repository's harness.
3. **`sources-verbatim`**: sources are copied unedited under `sources/`; interpretation lives in `notes/`, `research/`, `qa/`.
4. **Verification labels**: ✔ read at the primary source, ◐ search summary or secondary, ✖ the claim was wrong (corrected), [synthesis] opinion without an external source; evidence grading in `evidence-register.md`. State what was read (abstract only or full text).
5. **`ask-dont-assume`**: ask early, but do not interrupt a running flow with questions. Mark assumptions explicitly.
6. **`mnemonic-names`**: no numbered codes in chat or docs (only the harness task numbers 287 to 299 are kept). Questions, threads, mechanisms are referred to by short names.
7. **`living-example`**: show every concept on this repository's ImageGen project (`case-study-imagegen.md`).
8. **Answer in chat**, in Russian, in full from start to end; **no deltas** ("do not give a delta, describe from the beginning to the end"). The owner finds files hard to read in the interface: put the explanation in the message, the files are the record. Concept explainers marked deferred stay deferred.
9. **`english-before-merge`**: all Russian text is removed before merging to `main`; specs and documents will be built in English; the harness CI check of non-Latin text in `docs/` is postponed (return to it later).
10. **Rules vs enforcement**: advisory rules versus enforced checks (`notes/rules-vs-skills-vs-permissions.md`); the owner calls rules "steering", the word does not matter.
11. **Commits**: on branch `swf` only; end commit messages with the attribution lines the session reminder gives (`Co-Authored-By: ...`, `Claude-Session: ...`); push with `git push -u origin swf`; no pull request unless the owner asks.
12. **Reading the web**: `WebFetch` fails on many hosts; use Bash `curl` (works for most public pages, not `github.com` HTML or the GitHub API: use `raw.githubusercontent.com`), `WebSearch` is available; save fetched primary pages under `sources/` with a `PROVENANCE.md`.
13. Subagents only when the owner asks; a classifier denied `curl` in a subagent once and it was not worked around.

## 3. What exists in the repository (map)

- `definition.md` (v1.2: four conditions, executor profile, inputs and outputs, risk-class rules), `decisions.md` (log of owner decisions, newest at the bottom), `open-threads.md` (table of everything open), `catalog.md` (mechanisms, ~140), `case-study-imagegen.md` (living example), `evidence-register.md`, `human-org-parallels.md`, `concepts/` (deferred), `owner-notes.md`.
- `sources/` (verbatim): `tsf-lesson`, `warp-talk`, `owner-dialog` and `owner-dialog-questions` (the primary source: the owner's dialog, 43 questions), `always-on-agents`, `cloud-factory-demo/` (the Warp repository copy), `warp-talk-owner-notes` (handwritten notes, transcribed), `ai-engineer-talk-page/`, `warp-dev-site/`, `context-management/`, `spec-driven-development/`, `spec-research/`. Each folder has provenance.
- `notes/` per source; `qa/owner-dialog-walkthrough.md` (the walkthrough of the 43 questions), `qa/owner-session-answers.md`, `qa/tsf-lesson-questions.md`.
- `research/`: `factory-kits-*`, `scripted-harness-around-agents.md`, `classic-factory-adoption.md`, `context-management-practices.md`, `unambiguous-complete-specs.md`, `formal-specs-and-spec-testing.md`.
- `tasks/factory/` tasks 287 to 299, all status `todo` (compare autonomy ladders; compare factory kits by code; walk through the real flow; verify TSF origin; map always-on primitives to the harness; write the universal factory guide; design the human review cadence; error diversity for verification; scripted agent harness design; POC formalize ImageGen as models; classic factory adoption second pass; **298** ImageGen module map and regression guards; **299** small-model spec probe experiment).
- The scratchpad of the earlier session is gone; nothing in it is needed (the repository holds the source of truth).

## 4. Where we stopped

### 4.1 The walkthrough of the owner's 43 questions (method: one question at a time)
For each question show: the original quoted text, the owner's question, the aggregated checked answer with links, the ImageGen example, and a follow-up question. Source text of each is in `qa/owner-dialog-walkthrough.md` (generated earlier; the repo file is the source of truth), full list in its Index.

Done (7): `how-factories-work`, `old-vs-new-compare`, `twins-just-mocks`, `shapiro-levels-details`, `agents-manage-context`, `not-java-hibernate`, `specs-are-templates`.

**Next in order: question 8, `old-new-same-axis-objection`** (the owner: "go deeper here, so far everything except verification and risk is one and the same"). The previous answers already started it: determinism moves from generation to verification (`research/unambiguous-complete-specs.md`, last section). The remaining 35, in order: `remember-comparison`, `twin-vs-mock-state`, `twin-literally-mock`, `move-self-and-others-up`, `level-4-to-5-details`, `what-is-holdout`, `tacit-knowledge-extraction`, `humans-have-it-too`, `conflicting-sources-how-humans-resolve`, `split-agents-by-errors`, `humans-limited-window-too`, `how-agents-manage-context`, `how-to-write-specs`, `remember-quiet-vs-loud-failure`, `mocks-stubs-fakes-difference` (the taxonomy explanation is deferred by the owner: `test-doubles-taxonomy-explained`), `grow-spec-writing-skill`, `keeping-control`, `mutation-testing-as-proof`, `pilot-not-transferable`, `our-team-and-stack`, `how-author-defines-human-adds-nothing`, `small-system-vs-ours`, `graded-satisfaction-populism`, `other-ways-to-independence`, `twin-95-percent`, `restore-understanding`, `obituary-or-postmortem`, `cannot-fit-claim-too-strong`, `remember-people-answer-questions`, `type-rule-to-linter`, `code-review-unavoidable`, `cheap-failure-too-good`, `yes-walk-real-flow`, `remember-pre-mortem`, `remember-assumptions-list`.
Note: later questions in the walkthrough repeat or refine earlier ones; add answers to earlier threads at once, but every question must still be walked through.

### 4.2a Update 2026-11-21: task 299 was run
The owner answered (2026-11-20): run **both** options; the assistant pre-writes the hit rules ("ну давай"). Done: `docs/factory/experiments/small-model-spec-probe/` (`protocol.md` committed before the runs, `results.md`, `scores.csv`, `runs/`). Result: qwen 3B 0.13, haiku 0.56, sonnet 0.71 (hit = 1, partial = 0.5); the missing-sentence case (067) found by no model; heavy noise. Waiting for the owner: accept the recommendation (changed protocol, or drop the probe) and the one disputed score (haiku 097 run 5). Task 299 is in `review`. Next in the walkthrough is still `old-new-same-axis-objection`. The section 4.2 below is history.

### 4.2 The last assistant message and what it waits for (2026-11-18, answered)
The last message (2026-11-18) answered three questions (how seL4 proofs work: C is hand-written and parsed into Isabelle, not generated from it; how vericoding closes the gap: spec and code in one file, harness rebuilds the file from the fixed spec and checks bypass patterns such as `assume(false)`, `sorry`, `ensures true`, comment tricks; the checker is a deterministic program, not an agent or a reviewer, humans and agents review the spec) and reported the history search for task 299. It ended with two open items for the owner:
1. **Which small model for the experiment (task 299)?** Option 1 (recommended, announced as "will start unless you object"): a local model of about 3 billion parameters of another family than Claude, run on CPU in the sandbox (4 CPUs, 15 GB RAM, huggingface.co reachable; about an hour for six cases by five runs). Option 2: a small Claude model through subagents (fast, but the same family, and the subagents must be isolated from the repository, which contains the answers). **Nothing has been run yet.** The owner has not objected nor confirmed: confirm before spending the setup time.
2. **Who approves the "hit" rule for each case**: the owner, or the assistant pre-writes it and disputed ones go to the owner.

### 4.3 Open items waiting for the owner (all recorded in `open-threads.md`)
- Small-model spec probe (above); the owner cannot recall rolled-back fixes by memory, the history search found the touch and tap family (tasks 088, 162, 211, 214; pairs 097/159, 089/096 to verify): `case-study-imagegen.md`, task 299, task 298.
- `rules-placement`: the proposal (advisory rules go to the data plane, enforced rules are permissions and checks in the control plane) awaits confirmation; second question: rules that can be neither checked by a script nor expressed as a permission.
- Review design (what the owner's review hours go to), independence wording (provisional, revisit), reservations on the old-versus-new comparison ("later"), real-flow walkthrough (task 289), outbox design details, `acceptance-test-quality`, `spec-parser-generator-step`, `harness-as-function` (the agent returns a JSON delta in a fixed format, applied by the harness), `deterministic-path-for-simple-tasks`, `pseudocode-as-spec-level`, `gherkin-to-app-code`, `formal-proof-architecture-and-code` (first pass done), `warp-slides-infrastructure-part`, `transcript-compare`, low-code as heir, classic-factory second pass (297), scripted harness design (295), POC to formalize ImageGen (296), `english-rebuild-before-merge` and the harness CI check (deferred), mission-command thread (deferred), test-doubles taxonomy (deferred).

## 5. Key results so far (for orientation, details in the files)

- **Definition v1.2**: principals state intent and acceptance rules; executors (people or agents, no difference in kind) do everything between intent and verified result; four conditions: independent and measured verification, system-enforced boundaries, a feedback loop back to input, humans only at decision points and event- or evidence-triggered reviews (the factory's own rules are always reviewed by a human and an agent).
- **Decisions** worth knowing: irreversible external actions always go through a human; bug versus feature is decided by a human; risk-class list stays universal only; the owner reads ImageGen at about autonomy level 4 and works at about level 3 elsewhere; `stack-neutral`; `spec-and-template-no-big-difference` (the owner's position: a complete unambiguous spec is almost a template, one step remains: parser plus generator; counter-analysis in `research/unambiguous-complete-specs.md`); `imagegen-acceptance-not-independent` (Gherkin scenarios were written by the same agents as the code; no factory yet; main problem is regressions and acceptance-test quality).
- **ImageGen facts** (2026-11-17): about 3 weeks old, 462 commits (now ~480), 36 ADRs, 264 archived tasks, 737 C# files, 9 `.feature` files, `E2E.MockProvider`, `LiveCheck` with `BudgetGuard`; the owner has not read code since the first PR and verifies by installing and testing.
- **Warp**: control plane and data plane lists in the owner's notes come from a slide (confirmed), efficiency = software shipped / (tokens + human time), two loop levels; the warp.dev definition of a factory differs from the repository's (`notes/warp-site-and-ai-engineer-page.md`).
- **Research results**: scripted harness around agents (ACP does not enforce control; needs OS-level isolation and host-served tools); classic factory adoption (first pass); context management (repository as memory, files for large results with a fixed output contract); formal specs (cheap proofs by LLM are real for small units, the spec problem remains); small-model probe (ClarifyGPT-style sampling is established, the confound is capability versus ambiguity).

## 5a. Update 2026-11-23
- Task 299 was executed in another session (results in `experiments/small-model-spec-probe/results.md`, task report): local 3B model not useful (hit rate 0.13), Haiku 0.56, Sonnet 0.71; the missing-sentence case was found by no model; one disputed score for the owner (haiku, case 097, run 5). Section 4.2 item 1 is therefore answered by events (both a local model and a small Claude model were run, the assistant pre-writes the hit rules, disputed ones go to the owner).
- The owner started a digression on spec-driven development and spec trees: `research/spec-driven-development-approaches-and-tree.md`, thread `spec-tree-organization`. After the digression, return to the walkthrough (section 4.1).

## 6. First actions in the new session

1. Sync and read (section 1). 2. Tell the owner in a few sentences where we are. 3. Ask the two items of 4.2 (small model choice, who approves the hit rule), unless the owner already answered in the resume message. 4. If the owner says "go on" without answering, continue the walkthrough with `old-new-same-axis-objection` (section 4.1) and keep task 299 pending. 5. Update this file at the end of the session (section 4 changes), commit and push.
