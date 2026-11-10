---
id: 270
status: planned
assignee:
claimed:
created: 2026-11-09
---

# Translate the script messages, workflow comments and poc to English

## Description
Part of 268. Russian remains in: `scripts/*.sh` (comments and messages such as the error prefix and "all checks passed" of `check.sh`), `scripts/tests/*.sh`, the comments of `.github/workflows/build.yml`, `harness.yml`, `harness-audit.yml`, `ImageGen/ImageGen.E2E.Common/ci/allure-summary.sh` (the summary text), one file in `poc/`. Changing `scripts/` and `.github/workflows/` is allowed by the owner's request to translate everything (2026-11-09).

Depends on: —; coordinate with 269 (both edit `scripts/check-tasks.sh` and `scripts/archive-tasks.sh`): 270 translates only messages and comments there, 269 the parsing; whoever is second rebases.
Environment: Linux or Windows; workflow edits are checked by a CI run.

## Acceptance criteria
- Every message and comment of `scripts/**` is English (error prefix `ERROR:`, warning prefix `WARNING:`, the success line `Harness: all checks passed`). No logic changes.
- The workflow comments are English; the stale comment about the `en_US.UTF-8` locale and Cyrillic ranges in `harness.yml`/`harness-audit.yml` is corrected (the Latin check sets `LC_ALL=C.UTF-8` itself; whether the locale lines are still needed — check by a run, keep them if unsure).
- `allure-summary.sh` prints the summary in English; its consumers (`ui-e2e.yml`, `apple-ci.yml`) still work (a CI run).
- `poc/` README/text translated.
- `scripts`, `.github`, `poc` are added to `LATIN_PATHS`.

## Plan
1. `scripts/` file by file; after each: `bash scripts/check.sh` and the script's own smoke call (`scripts/list-tasks.sh`, `scripts/audit.sh`, `bash scripts/tests/*.sh`).
2. Workflow comments; `allure-summary.sh`; a CI run of `ui-e2e.yml` (workflow_dispatch).
3. `poc/`, `LATIN_PATHS`.

Files: `scripts/**`, `.github/workflows/build.yml`, `.github/workflows/harness.yml`, `.github/workflows/harness-audit.yml`, `ImageGen/ImageGen.E2E.Common/ci/allure-summary.sh`, `poc/**`.

### Verification
```
bash scripts/check.sh
bash scripts/audit.sh
bash scripts/tests/release-notes-empty-previous.sh
bash scripts/tests/check-latin.sh
# GitHub Actions: build, harness-check and ui-e2e green
```

### Execution order (for Luna)
- First read only the files of the "Files" line; work file by file and commit in small steps (one folder per commit).
- Translate without changing the meaning: do not shorten, do not "improve" the content, do not fix facts — a wrong fact becomes a comment or a new task, not a silent edit.
- Keep identifiers, paths, commands, ids, numbers, links and quoted UI texts unchanged; translate only the prose.
- If something is ambiguous, keep the original meaning and write a question in "Comments".
- After each commit run `bash scripts/check.sh`. In the "Report": what was translated, which paths were added to `LATIN_PATHS`, what stayed as an exception and why.

## Report

## Comments

### 2026-11-09, claude-bdd-plan
Part of 268. The plan waits for approval.
