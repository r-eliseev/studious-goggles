---
id: 268
status: planned
assignee:
claimed:
created: 2026-11-09
---

# Translate the whole repository to English

## Description
The owner's decision of 2026-11-09: everything in the repository is in English, and the harness checks that the documentation is at least in Latin script. Done on 2026-11-09 in the chat session: `docs/`, `AGENTS.md`, `README.md` are translated, `scripts/check-latin.sh` checks `AGENTS.md`, `CLAUDE.md`, `README.md` and `docs/` (`LATIN_PATHS`), `docs/conventions.md` has the "Language" section.

This is the umbrella task: it is done when `LATIN_PATHS` covers the whole repository. The work is split into subtasks:
- 269 — `tasks/` (all task files, `tasks/_template.md`, `tasks/DONE.md`) and the section names the task scripts parse;
- 270 — `scripts/` (messages and comments), `.github/workflows/` comments, `poc/`;
- 271 — the code in `ImageGen/`: comments, exception messages, test names and test texts.

State on 2026-11-09 (files with Cyrillic): `tasks/` 59, `scripts/` 13, `.github/workflows/` 3, `poc/` 1, `ImageGen/` 35 text files (fonts and images are binary and are skipped by the check).

Depends on: —
Environment: Linux or Windows.

## Acceptance criteria
- 269, 270, 271 are done.
- `LATIN_PATHS` in `scripts/check-latin.sh` is `.` (the whole repository, `git ls-files`); `LATIN_EXCEPTIONS` lists only test data that checks non-English input, each with a reason.
- `AGENTS.md`, "Language": the sentence about files not translated yet is removed; `docs/conventions.md`, "Language": the sentence about task 268 is removed.
- `scripts/check.sh` passes; a file with Cyrillic outside the exceptions makes it fail anywhere in the repository.

## Plan
1. Wait for 269, 270, 271 (they may go in parallel: their files do not overlap).
2. Set `LATIN_PATHS=(.)`, run the check, fix leftovers (or create a task for them).
3. Remove the transition sentences from `AGENTS.md`, `docs/conventions.md`, `docs/tasks.md` ("Section names").

Files: `scripts/check-latin.sh`, `AGENTS.md`, `docs/conventions.md`, `docs/tasks.md`.

### Verification
```
bash scripts/check.sh
bash scripts/tests/check-latin.sh
```

## Report

## Comments

### 2026-11-09, claude-bdd-plan
Created at the owner's request "create a separate task to translate everything to English, translate the documentation now, small things — tasks for Luna". The plan waits for approval.
