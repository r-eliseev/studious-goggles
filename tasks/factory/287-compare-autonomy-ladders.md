---
id: 287
status: todo
assignee:
claimed:
created: 2026-11-17
---

# Compare autonomy ladders and choose how the factory sets the human mode

## Description
Part of the software-factory concept (`docs/factory/`). Three descriptions of "how much the human is involved" exist and have not been compared:
- Shapiro's levels 0-5, per system or per developer, gated by "who reads the code" (source 03, L221-258; the owner is at level 3);
- the "in / near / out of the loop" modes of the TSF lesson (source 01, claim C2; Q4 in `docs/factory/notes/01-tsf-lesson-2.md`);
- the per-action ladder (L0-L6, called LoopRails in a web search snippet, `docs/factory/qa/01-questions-on-source-01.md`, section B): found only as a search summary, the original was not opened.
The owner's remarks: the level belongs to a task class, service or repository, not to a person (source 03, L246, L571, L1406, L1516); today every task passes the **planner agent**, not the human, and plan approval is a standing instruction (`docs/status.md`, ADR 0033); candidate notes N1-N3 in `docs/factory/owner-notes.md`.

Depends on: none.
Environment: any (reading and writing documents; web access for the LoopRails original).

## Acceptance criteria
- The original description of the per-action ladder is found and read (not a search snippet), with its link and exact level definitions quoted; if it cannot be found, the Report says so.
- A table compares the three ladders and this repository's harness (ADR 0032, 0033, 0034, 0036): axis (system / task class / action), what defines each level, who decides, what lifts a level (verification), what lowers it.
- For each action of the planner and executor (write a plan, approve a plan, amend a plan, implement, review, merge, archive, change the harness, change keys handling or the data schema) the table states today's mode and a proposed mode with the check that would justify it.
- The Report lists decisions needed from the owner; it does not change `docs/tasks.md`, ADRs or `scripts/`.

## Analysis

## Plan

## Report

## Review

## Comments

### 2026-11-17, planner-session
Created from the owner's request "add a task, we will need to compare". Analysis and Plan are not written yet (status `todo`).
