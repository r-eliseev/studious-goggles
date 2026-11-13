---
id: 278
status: review
assignee:
claimed:
created: 2026-11-13
---

# Harness scripts: legacy Russian names must not depend on the locale

## Description
Task 270 replaced the Cyrillic literals of the harness parsers with `printf '\uXXXX'`. Bash expands `\u` escapes only when the locale is UTF-8. Under `LANG=` / `LC_CTYPE=POSIX` the strings stay literal (`Пл...`), so the legacy section names never match.

Evidence (2026-11-13, `main` at `838180f`, a container with `LC_CTYPE="POSIX"`):
- `bash scripts/check.sh` → `ERROR: tasks/backlog/260-acceptance-session-models.md: status 'review', but the Plan section is empty`, the same for its Report and for 277's Report; both cards have filled `## План` / `## Отчёт` sections.
- `bash -x scripts/check-tasks.sh` shows `awk -v 'h=План' ...`.
- `LC_ALL=C.UTF-8 bash scripts/check.sh` → `Harness: all checks passed`. CI is green only because the runners use a UTF-8 locale.
Affected: `scripts/check-tasks.sh` lines 9–14, `scripts/submit-task.sh` line 16, `scripts/check-docs.sh` lines 43–45.

Depends on: —.
Environment: Linux.

## Acceptance criteria
- The legacy strings are built from UTF-8 byte escapes (`printf '\xD0\x9F...'`), which Bash expands in every locale; the source stays Latin-only (`scripts` is in `LATIN_PATHS`). No other behavior change.
- A new regression `scripts/tests/locale-independent.sh` fails before the fix and passes after it: in a temporary copy (as `scripts/tests/task-roles.sh` does) a `review` card with the Russian headings `## План` and `## Отчёт` filled passes `env LC_ALL=C bash scripts/check-tasks.sh`; `env LC_ALL=C bash scripts/submit-task.sh` accepts an `in-progress` card whose `## Отчёт` is filled. The Cyrillic test input is produced with byte escapes too, so the test file needs no Latin exception.
- `scripts/check.sh` runs the new test like the other tests (`>/dev/null || rc=1`).
- `env LC_ALL=C bash scripts/check.sh` and `env LC_ALL=C.UTF-8 bash scripts/check.sh` both print `Harness: all checks passed` on the branch.

## Analysis
- The pattern is in three files only (`grep -rn '\\u04' scripts/`). `check-docs.sh` uses its strings inside a `grep -E` bracket expression `[$task_initials]`: with byte strings under the C locale a bracket of two 2-byte letters matches single bytes, which changes the meaning — rewrite that part as an alternation `(З|з)` built from the byte strings, keep the rest of the regex.
- Task 269 later removes the Russian alternatives from `check-tasks.sh` entirely; this task goes first because `main` is red under a POSIX locale. 269 must rebase onto it (noted in 269).
- Changing `scripts/` is allowed: the owner asked on 2026-11-13 to create fix tasks for every problem found in the review.

## Plan
1. Write `scripts/tests/locale-independent.sh`; run it: it fails with "Plan section is empty" (the reason above).
2. Replace the `\u` escapes with byte escapes in `check-tasks.sh` and `submit-task.sh`; the test passes.
3. `check-docs.sh`: byte escapes and the alternation; `bash scripts/tests/check-docs.sh` still passes; add one case to it only if it does not cover the architecture scan (check first).
4. Add the test to `scripts/check.sh`; run the verification under both locales.

Files: `scripts/check-tasks.sh`, `scripts/submit-task.sh`, `scripts/check-docs.sh`, `scripts/check.sh`, `scripts/tests/locale-independent.sh` (new), `scripts/tests/check-docs.sh`.

### Verification
```
env LC_ALL=C bash scripts/tests/locale-independent.sh
env LC_ALL=C bash scripts/check.sh
env LC_ALL=C.UTF-8 bash scripts/check.sh
bash scripts/tests/task-roles.sh
bash scripts/tests/check-docs.sh
```

## Report

### Changes
- Replaced locale-sensitive printf Unicode escapes with UTF-8 byte escapes in check-tasks.sh, submit-task.sh and check-docs.sh.
- Rewrote the architecture scan's Cyrillic initial match as an alternation so it works with byte strings under the POSIX locale.
- Added scripts/tests/locale-independent.sh and registered it in scripts/check.sh. Added a POSIX-locale architecture-scan case to scripts/tests/check-docs.sh.

### Verification
- Confirmed the new regression failed before the fix because check-tasks.sh treated the legacy Russian Plan as empty under LC_ALL=C.
- env LC_ALL=C bash scripts/tests/locale-independent.sh — passed.
- env LC_ALL=C bash scripts/tests/check-docs.sh — passed.
- env LC_ALL=C bash scripts/check.sh — passed.
- env LC_ALL=C.UTF-8 bash scripts/check.sh — passed.
- bash scripts/tests/task-roles.sh — passed.
- bash scripts/tests/check-docs.sh — passed.
- bash scripts/check-latin.sh — passed.

No differences from the approved plan.

## Review

## Comments

### 2026-11-13, claude-planner
Created from the review of task 270 (PR #101) at the owner's request to open fix tasks for every problem found.
Plan approved (standing instruction of 2026-11-07: "approve all tasks that will come").
