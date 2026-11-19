---
id: 269
status: done
assignee:
claimed:
created: 2026-11-09
---

# Translate the tasks and the task section names to English

## Description
Part of 268. Most task files are English already (see the Report). What remains, as of 2026-11-17: this card's own history (it quoted the old Russian headings), task 271's card (done, to be archived by the planner), and the harness side — the legacy Russian section names still accepted by the scripts, built from UTF-8 byte escapes since task 278: `scripts/check-tasks.sh` lines 9–14 (`legacy_plan`, `legacy_report`, `legacy_approval`, `legacy_description`, `legacy_acceptance`, `legacy_analysis`) and `scripts/submit-task.sh` line 16 (`legacy_report`). The documentation already describes the English names (`docs/tasks.md`, "Section names").

Amended 2026-11-17 by the planner: the planner translated this card and archives 271, so no executor has to edit a protected section of its own card.

Depends on: —
Environment: Linux or Windows.

## Acceptance criteria
- No task file contains Cyrillic letters: `python3 -c` or `grep -P` check over `tasks/` returns nothing (write the command used in the Report). If a card still has Russian text, translate it (headings and lines to the English names, prose to English; front matter unchanged; dated comment headers kept). Russian UI literals become the current English UI text from `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx` (amendment of 2026-11-13).
- `tasks/DONE.md` is English; `scripts/archive-tasks.sh` writes English lines.
- `scripts/check-tasks.sh` and `scripts/submit-task.sh` accept only the English sections and approval phrase: the `legacy_*` variables and their alternatives are removed, in the same commit as `tasks` is added to `LATIN_PATHS` in `scripts/check-latin.sh`.
- `scripts/tests/locale-independent.sh` (task 278) tests the legacy headings; with the legacy names gone it is removed from `scripts/check.sh` and deleted, or rewritten to check that the English headings work under `LC_ALL=C` — choose the rewrite if it still guards something real, and say which in the Report.
- `scripts/tests/task-roles.sh` and `scripts/tests/release-notes-empty-previous.sh` pass.
- `docs/tasks.md`, "Section names": the transition paragraph is removed.

## Analysis
- `grep -n legacy_ scripts/*.sh scripts/tests/*.sh` lists every use. `scripts/check-docs.sh` also builds Russian words from byte escapes for the architecture scan (task 278); that is a documentation check, not a task parser, and stays.
- Archived task files are deleted by `scripts/archive-tasks.sh`, so done tasks need no translation once archived.

## Plan
1. Find remaining Cyrillic in `tasks/`; translate what is left.
2. Remove the legacy names from `check-tasks.sh` and `submit-task.sh`; adapt or remove `locale-independent.sh`; add `tasks` to `LATIN_PATHS`; one commit.
3. `docs/tasks.md` paragraph; verification.

Files: `tasks/**`, `scripts/check-tasks.sh`, `scripts/submit-task.sh`, `scripts/check-latin.sh`, `scripts/check.sh`, `scripts/tests/locale-independent.sh`, `scripts/archive-tasks.sh`, `docs/tasks.md`.

### Verification
```
bash scripts/check.sh
env LC_ALL=C bash scripts/check.sh
bash scripts/tests/task-roles.sh
bash scripts/tests/release-notes-empty-previous.sh
TASKS_BRANCH=main scripts/list-tasks.sh --main approved
```

### Execution order
- Translate without changing meaning; keep identifiers, paths, commands, ids, numbers and links.
- After each commit run `bash scripts/check.sh`. In the Report: what was translated, the Cyrillic check command and its empty output, and the decision on `locale-independent.sh`.

## Report

### 2026-11-19, codex-20261117
- Confirmed `rg -n --pcre2 '\p{Cyrillic}' tasks` returns no matches; `tasks/DONE.md` is English, and `scripts/archive-tasks.sh` writes an English header and task rows.
- Removed legacy section and approval parsing from `scripts/check-tasks.sh` and `scripts/submit-task.sh`, added `tasks` to `LATIN_PATHS`, removed the transition paragraph from `docs/tasks.md`, and rewrote `scripts/tests/locale-independent.sh` to check English headings and reject legacy headings under `LC_ALL=C`.
- TDD: the rewritten locale regression failed before the parser changes because the old headings were accepted; it passes after the changes.
- Verification passed: `bash scripts/check.sh`, `env LC_ALL=C bash scripts/check.sh`, `bash scripts/tests/task-roles.sh`, `bash scripts/tests/release-notes-empty-previous.sh`, `TASKS_BRANCH=main scripts/list-tasks.sh --main approved`, `bash scripts/tests/locale-independent.sh`, `bash scripts/check-tasks.sh`, `bash scripts/check-latin.sh`, and `git diff --check`.
- Plan followed; no behavior or architecture documentation was needed.

### 2026-11-13, codex-gpt6
- Translated the task template and cards 030, 046, 140, 168, 198, 230, 236, 237, and 245 in small batches. The 245 branch has no remote task branch or unmerged card edit.
- Also translated cards 248 and 250; neither has a remote task branch.
- Translated card 249 after confirming it has no remote task branch.
- Translated cards 246 and 247 after confirming that neither has a remote task branch; retained 247's `blocked` status and existing reports.
- Translated card 243 last; its remote branch had no unmerged edits to the task card.
- Translated `tasks/v0.1/016-skeleton-manual-check.md`; retained its quoted Russian UI labels.
- Translated every title and status label in `tasks/DONE.md`; preserved task ids, dates, statuses, and paths. `scripts/check.sh` passed.
- Translated acceptance cards 258, 259, 262, 263, 264, and 265; these have no remote task branches.
- After each committed batch, `bash scripts/check.sh` passed. The new-task template was rendered in a scratch copy and matched the English headings.
- Cards 235 and 255 remain untouched because their remote task branches contain unmerged edits to those same cards. Card 243's remote branch has no card diff; it remains for the final translation pass as required by the plan.
- Also left cards 256, 257, 260, 261, 266, 272, 274, 275, 276, and 277 untouched because their status differs from `origin/main` in this branch; the harness validates the fixed sections during these status transitions. The added historical task 276/277 cards also remain owned by the work that introduced them.
- Quoted Russian UI/status literals remain unchanged as required by the plan (for example, in task 016 and cards 046, 198, 243, 248, 258, 259, 262–265). This conflicts with the current `check-latin.sh` rule, which permits exceptions only for non-English test data. Resolve this before adding `tasks` to `LATIN_PATHS`; a plan/owner decision is needed on whether those quoted literals may be translated or whitelisted.

## Review

Reviewed on 2026-11-19 against the plan and acceptance criteria. Confirmed task markdown has no Cyrillic (`rg -n --pcre2 '\p{Cyrillic}' tasks` returned no matches), `tasks/DONE.md` and archive output are English, legacy section and approval parsing is removed, `tasks` is in `LATIN_PATHS`, and the locale regression accepts English headings while rejecting legacy headings under `LC_ALL=C`. All listed checks pass, including both default and C-locale harness runs. Verdict: done.

## Comments

### 2026-11-13, codex-gpt6
The approved plan has incompatible requirements for the Latin-only check. `Execution order` says to keep quoted UI text unchanged, and the translated task cards retain required Russian UI/status literals (for example `tasks/backlog/258-acceptance-cancel-recovery.md` uses `"cancelled" (Russian in the card at the time)` as an expected application status). The acceptance criteria also require adding all of `tasks/` to `LATIN_PATHS`, while `scripts/check-latin.sh` rejects every non-Latin letter and allows exceptions only for non-English test data. Therefore the check cannot pass without either changing the task content contrary to the approved translation rule or widening the checker exceptions contrary to its current documented purpose.

Proposed plan change: explicitly permit exact quoted UI/status literals in task cards through a narrowly scoped allowlist for `tasks/` (separate from test-data exceptions), while keeping the rest of task files under `LATIN_PATHS`; alternatively, approve translating those literals and accepting that task examples no longer match the current UI. Please choose the intended rule and update the plan before implementation continues.


### 2026-11-09, claude-bdd-plan
Part of 268. Changing `scripts/` is allowed by the owner's request to translate everything (2026-11-09). The plan waits for approval.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan)
Plan approved ("approve all tasks with plans").

### 2026-11-13, claude-planner — plan amendment
Answers the Latin-check conflict of 2026-11-13. Verified: the app UI is English — `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx` is the only resource file (`StatusCancelled` = "Cancelled", `StatusFailed` = "Error", `StatusFiltered` = "Filtered by the provider"). The quoted Russian literals in task cards (the Russian words for "cancelled", "done", "error", "filtered" and the labels in 016, 046, 198, 243, 248, 258, 259, 262–265) are leftovers of the old Russian UI, not current UI text. Rule: replace each such literal with the current English UI text from `AppResources.resx` (search by meaning; if no resource matches, translate it and add "(UI text not found)" after it). No Latin exception for `tasks/`.
Also: 235 and 255 no longer block the translation — 255's branch is merged, and the planner rewrote 235's card in English on 2026-11-13 (its stale branch holds only superseded card edits). Cards changed by the planner in this sync may already be English; translate what remains. Task 278 changes `scripts/check-tasks.sh` first; start from a `main` that contains 278.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").

### 2026-11-15, codex-20261115
Translated task cards 255–265 where no other branch has unmerged edits: 255–259 and 261–265 are now English; cards 256, 257, and 261 were checked against their remote task branches and were identical to `origin/main` before translation. `git diff --check` and `bash scripts/check.sh` pass.

Remaining cards include this task itself and task 271. The executor rules and `scripts/check-tasks.sh` prohibit changing Description, Acceptance criteria, Analysis, or Plan while a task moves from `in-progress` to `blocked` or `review`; this prevents translating task 269's own Russian Description and Analysis while executing it. Task 271 has a remote task branch with unmerged edits to its card, so it must be translated by that branch's owner. The English-only task path and parser changes cannot be completed while these files remain untranslated.

### 2026-11-15, codex-20261115
The task cannot satisfy its acceptance criteria under the current executor guard: task 269's own protected Description/Analysis sections still contain Russian, and the checker rejects changes to those sections when the executor submits or blocks the task. Proposed plan change: explicitly allow translating the current task's protected sections as a final step in the same commit that marks it `review`, or specify a planner-mediated status/branch sequence that permits that final translation without weakening the rule for other tasks. Keep task 271 untouched until its branch owner has no unmerged card edits. After the planner amends and approves the plan, finish task 269 and add `tasks/` to `LATIN_PATHS` only once every remaining task card is English.

### 2026-11-17, claude-planner — plan amendment
Answers the self-block of 2026-11-15: the planner rewrote this card in English (the Russian heading names it quoted are now described in words), and task 271 is reviewed and archived, so no card the executor must edit carries protected Russian text. The plan above covers only what remains.
Plan amendment approved (standing instruction of 2026-11-07: "approve all tasks that will come").
