---
id: 269
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-13T06:28:52Z
created: 2026-11-09
---

# Translate the tasks and the task section names to English

## Description
Part of 268. The task files (`tasks/backlog/*.md`, `tasks/v0.1/*.md`, `tasks/_template.md`, `tasks/DONE.md`) are in Russian, and the task scripts parse some Russian section names: `scripts/check-tasks.sh` checks that "План"/"Отчёт" (Plan/Report) are filled (since 2026-11-09 it also accepts `## Plan` and `## Report`). The documentation already describes the English names (`docs/tasks.md`, "Section names").

The section names map like this: Описание → Description, Критерии готовности → Acceptance criteria, Анализ → Analysis, План → Plan, Отчёт → Report, Комментарии → Comments; lines: «Зависит от» → "Depends on", «Среда» → "Environment", «Файлы:» → "Files:", «Проверка» → "Verification", «Порядок исполнения (для Luna)» → "Execution order (for Luna)".

Depends on: —
Environment: Linux or Windows.

## Acceptance criteria
- `tasks/_template.md` uses the English section names; `scripts/new-task.sh` renders it unchanged.
- `scripts/check-tasks.sh` checks only `## Plan` and `## Report` (the Russian alternatives are removed) — in the same commit as the last task file is translated, so that no commit leaves the check broken.
- Every task file is translated: the front matter is unchanged (`id`, `status`, `assignee`, `claimed`, `created`), the headings and lines use the mapping above, the prose is English. Comments keep their dated headers (`### YYYY-MM-DD, <name>`); a comment's text is translated, the author stays.
- Tasks claimed by another agent (`assignee` set, `in-progress`, or a `task/NNN-*` branch on origin — today 243, 245, 246, 247, 249) are translated last and only after checking that the branch has no unmerged edits of the same file; otherwise leave them and list them in the "Report" (the owner of the branch translates them).
- `tasks/DONE.md`: the titles and statuses of the lines are translated; `scripts/archive-tasks.sh` writes new lines in English (`unknown` instead of the Russian placeholder).
- `scripts/release-notes.sh` takes task titles from the files: check that the release notes for the last tag still list the tasks (`bash scripts/tests/release-notes-empty-previous.sh`).
- `docs/tasks.md`, "Section names": the transition paragraph is removed.
- `tasks` is added to `LATIN_PATHS` in `scripts/check-latin.sh`.

## Analysis
- Parsers: `scripts/check-tasks.sh` (`filled`), `scripts/tasks-lib.sh` (front matter only), `scripts/archive-tasks.sh` (the DONE line), `scripts/release-notes.sh` (titles), `scripts/list-tasks.sh` (front matter). Check them all with `grep -n` for Russian words before starting.
- About 60 files, ≈550 KB. Translate in batches by number range (001–099, 100–199, 200–251, 252–271), one commit per batch.

## Plan
1. Template and `new-task.sh` check (`scripts/new-task.sh backlog probe` into a scratch copy, not committed).
2. Batches of task files.
3. `DONE.md` and `archive-tasks.sh`.
4. `check-tasks.sh` English only, `LATIN_PATHS`, `docs/tasks.md`.

Files: `tasks/**`, `scripts/check-tasks.sh`, `scripts/archive-tasks.sh`, `scripts/check-latin.sh`, `docs/tasks.md`.

### Verification
```
bash scripts/check.sh
bash scripts/tests/release-notes-empty-previous.sh
TASKS_BRANCH=main scripts/list-tasks.sh --main approved
```

### Execution order (for Luna)
- First read only the files of the "Files" line; work file by file and commit in small steps (one folder per commit).
- Translate without changing the meaning: do not shorten, do not "improve" the content, do not fix facts — a wrong fact becomes a comment or a new task, not a silent edit.
- Keep identifiers, paths, commands, ids, numbers, links and quoted UI texts unchanged; translate only the prose.
- If something is ambiguous, keep the original meaning and write a question in "Comments".
- After each commit run `bash scripts/check.sh`. In the "Report": what was translated, which paths were added to `LATIN_PATHS`, what stayed as an exception and why.

## Report

### 2026-11-13, codex-gpt6
- Translated the task template and cards 030, 046, 140, 168, 198, 230, 236, 237, and 245 in small batches. The 245 branch has no remote task branch or unmerged card edit.
- Also translated cards 248 and 250; neither has a remote task branch.
- After each committed batch, `bash scripts/check.sh` passed. The new-task template was rendered in a scratch copy and matched the English headings.
- Cards 235 and 255 remain untouched because their remote task branches contain unmerged edits to those same cards. Card 243's remote branch has no card diff; it remains for the final translation pass as required by the plan.
- The exact quoted Russian UI labels in cards 046 and 198 remain unchanged as required by the plan. This conflicts with the current `check-latin.sh` rule, which permits exceptions only for non-English test data. Resolve this before adding `tasks` to `LATIN_PATHS`; a plan/owner decision is needed on whether those quoted UI labels may be translated or whitelisted.

## Comments

### 2026-11-09, claude-bdd-plan
Part of 268. Changing `scripts/` is allowed by the owner's request to translate everything (2026-11-09). The plan waits for approval.

### 2026-11-10, owner (in the chat, recorded by claude-bdd-plan)
Plan approved ("approve all tasks with plans").
