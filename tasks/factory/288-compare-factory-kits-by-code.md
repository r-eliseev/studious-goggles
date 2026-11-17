---
id: 288
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Compare public software-factory kits by their source code against this harness

## Description
Six public kits were described from README pages only (`docs/factory/research/01-factory-skills-landscape.md`): disler/super-simple-software-factory, riquenandez/ts-factory, dwightfowler-tema/factory-setup, PaddyMc/software-factory, juan-campuzano/minifactory, geut/factory-skills. The owner asked (2026-11-17) to read their code in helper agents without installing or running anything, and then to compare. Helper-agent reports are collected in `docs/factory/research/02-factory-kits-code-reading.md` (written by the session that launched them); this task covers the comparison against this repository's harness (planner and executor roles, task files, `scripts/`, ADR 0032-0036).

Depends on: none.
Environment: any; nothing is installed or run.

## Acceptance criteria
- `docs/factory/research/02-factory-kits-code-reading.md` exists with one section per kit: size, how it really works, README-vs-code differences, what could not be verified.
- A comparison table: for each kit and for this harness, who owns sequencing (code or agent), how agent claims are verified, how write permissions are enforced, where state lives, how budgets and retries are limited, how install/upgrade works, where the human gates are.
- A list of at most ten concrete ideas worth adopting here, each with its cost and the harness part it would change, and a list of what should not be copied and why.
- Nothing is installed, run or copied into the repository's product code; no file under `scripts/` or `.github/` changes.

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Created with the code-reading step started by the owner's request ("read the code in subagents, install nothing"). Status stays `todo` until the reports are merged into research 02 and the comparison is planned.
