---
id: 288
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Compare public software-factory kits by their source code against this harness

## Description
Six public kits were described from README pages only (`docs/factory/research/factory-kits-readme-level.md`): disler/super-simple-software-factory, riquenandez/ts-factory, dwightfowler-tema/factory-setup, PaddyMc/software-factory, juan-campuzano/minifactory, geut/factory-skills. The owner asked (2026-11-17) to read their code in helper agents without installing or running anything, and then to compare. Helper-agent reports are collected in `docs/factory/research/factory-kits-code-reading.md` (written by the session that launched them); this task covers the comparison against this repository's harness (planner and executor roles, task files, `scripts/`, ADR 0032-0036).

Depends on: none.
Environment: any; nothing is installed or run.

## Acceptance criteria
- `docs/factory/research/factory-kits-code-reading.md` exists with one section per kit: size, how it really works, README-vs-code differences, what could not be verified.
- A comparison table: for each kit and for this harness, who owns sequencing (code or agent), how agent claims are verified, how write permissions are enforced, where state lives, how budgets and retries are limited, how install/upgrade works, where the human gates are.
- A ranked list of candidate ideas for this harness, each with its cost and the harness part it would change. Nothing is dropped for being inapplicable here: every mechanism from the kits is recorded in `docs/factory/catalog.md` with an applicability column (owner's principle `document-everything`, 2026-11-17).
- Nothing is installed, run or copied into the repository's product code; no file under `scripts/` or `.github/` changes.

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Created with the code-reading step started by the owner's request ("read the code in subagents, install nothing"). Status stays `todo` until the reports are merged into research 02 and the comparison is planned.

### 2026-11-17, planner-session
All six code-reading reports are in `docs/factory/research/factory-kits-code-reading.md` (sections 1-6), with a draft cross-kit table and a ranked idea list in section 7. Acceptance criteria 1 and 2 are drafted, criterion 3 (at most ten ideas, plus what not to copy) is drafted; the comparison column for "this harness" is from `docs/tasks.md` and ADR 0032 only and was not audited against the scripts. Left `todo`: the planner still has to verify the harness column and decide with the owner.

### 2026-11-17, planner-session
Criterion 3 amended on the owner's instruction (principle `document-everything`: document everything, even what does not apply to the current harness; the concept is universal). The amendment replaces "a list of what should not be copied" by the catalog with an applicability column.

### 2026-11-18, planner-session
A seventh kit is added as a source: `warpdotdev-demos/cloud-factory-demo` (`docs/factory/sources/cloud-factory-demo/`, first notes in `docs/factory/notes/cloud-factory-demo.md`). The comparison of the kits is extended by it: its controller is GitHub Actions workflows, its state lives in labels and pull requests, it splits read and write across jobs, and it has a daily skill-improvement loop.
